﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Outcompute.Trader.Trading.Algorithms.Positions;

/// <summary>
/// Automatically resolves positions on a symbol by calculating lifo pnl from all trades on it.
/// </summary>
internal partial class AutoPositionResolver : IAutoPositionResolver
{
    private readonly ILogger _logger;
    private readonly AutoPositionResolverOptions _options;

    public AutoPositionResolver(ILogger<AutoPositionResolver> logger, IOptions<AutoPositionResolverOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    private static string TypeName => nameof(AutoPositionResolver);

    private sealed record Map(OrderQueryResult Order, AccountTrade Trade)
    {
        public decimal RemainingExecutedQuantity { get; set; }
    }

    public AutoPosition Resolve(Symbol symbol, ImmutableSortedSet<OrderQueryResult> orders, ImmutableSortedSet<AccountTrade> trades, DateTime startTime)
    {
        // todo: validate that the symbols are consistent between all arguments

        var watch = Stopwatch.StartNew();

        var (mapping, commissions) = Combine(symbol, orders.Where(x => x.Time >= startTime), trades.Where(x => x.Time >= startTime));

        // now prune the significant trades to account interim sales
        var subjects = mapping.ToList();

        // keep track of profit
        var profits = ImmutableList.CreateBuilder<ProfitEvent>();

        // now match sale leftovers using lifo
        // the sales may not fill completely using the buys due to selling from savings and buy market orders to help fix bugs
        for (var i = 0; i < subjects.Count; ++i)
        {
            // loop through sales forward
            var sell = subjects[i];
            if (sell.Order.Side == OrderSide.Sell && sell.RemainingExecutedQuantity > 0m)
            {
                // loop through buys in lifo order to find matching buys
                for (var j = i - 1; j >= 0; --j)
                {
                    var buy = subjects[j];
                    if (buy.Order.Side == OrderSide.Buy && buy.RemainingExecutedQuantity > 0m)
                    {
                        // remove as much as possible from the buy to satisfy the sale
                        var take = Math.Min(buy.RemainingExecutedQuantity, sell.RemainingExecutedQuantity);
                        buy.RemainingExecutedQuantity -= take;
                        sell.RemainingExecutedQuantity -= take;

                        // create a profit event
                        profits.Add(new ProfitEvent(
                            symbol,
                            sell.Trade.Time,
                            buy.Order.OrderId,
                            buy.Trade.Id,
                            sell.Order.OrderId,
                            sell.Trade.Id,
                            take,
                            buy.Trade.Price,
                            sell.Trade.Price));

                        // if the sale is filled then we can break early
                        if (sell.RemainingExecutedQuantity == 0) break;
                    }
                }

                // if the sale was still not filled then force close it
                // we assume the remaining assets used to fullfil the sale came either savings or market conversions
                // both of which we cant track here
                if (sell.RemainingExecutedQuantity != 0)
                {
                    // clear the sale
                    LogCouldNotFillOrder(TypeName, symbol.Name, sell.Order.Type, sell.Order.Side, sell.Order.OrderId, sell.RemainingExecutedQuantity, symbol.BaseAsset);

                    sell.RemainingExecutedQuantity = 0m;
                }
            }
        }

        // keep only buy orders with some quantity left to sell
        var positions = subjects
            .Where(x => x.Order.Side == OrderSide.Buy && x.RemainingExecutedQuantity > 0m)
            .GroupBy(x => x.Order)
            .Select(x => new Position(
                symbol,
                x.Key.OrderId,
                x.Sum(y => y.Trade.Price * y.Trade.Quantity) / x.Sum(y => y.Trade.Quantity),
                x.Sum(y => y.RemainingExecutedQuantity),
                x.Key.Time))
            .OrderBy(x => x.Symbol)
            .ThenBy(x => x.OrderId)
            .ToList();

        // apply balance corrections
        if (_options.BalanceCorrections.TryGetValue(symbol.Name, out var correction))
        {
            positions.Reverse();

            while (correction < 0 && positions.Count > 0)
            {
                var position = positions[^1];
                var take = Math.Min(position.Quantity, Math.Abs(correction));

                if (take >= position.Quantity)
                {
                    positions.RemoveAt(positions.Count - 1);
                }
                else
                {
                    positions[^1] = position with { Quantity = position.Quantity - take };
                }

                correction += take;
            }

            if (correction > 0)
            {
                // noop for now
            }

            positions.Reverse();
        }

        watch.Stop();
        if (watch.Elapsed >= _options.ElapsedTimeWarningThreshold)
        {
            LogIdentifiedPositions(TypeName, symbol.Name, positions.Count, watch.ElapsedMilliseconds);
        }

        return new AutoPosition
        {
            Symbol = symbol,
            Positions = positions.ToPositionCollection(),
            ProfitEvents = profits.ToImmutable(),
            CommissionEvents = commissions
        };
    }

    private (SortedSet<Map> Mapping, ImmutableList<CommissionEvent> Commissions) Combine(Symbol symbol, IEnumerable<OrderQueryResult> orders, IEnumerable<AccountTrade> trades)
    {
        var lookup = trades.ToLookup(x => x.OrderId);

        var mapping = new SortedSet<Map>(MapComparer.Instance);
        var commissions = ImmutableList.CreateBuilder<CommissionEvent>();

        foreach (var order in orders)
        {
            var quantity = 0m;

            foreach (var trade in lookup[order.OrderId])
            {
                // map the order to the trade so we have info on both
                var map = new Map(order, trade)
                {
                    RemainingExecutedQuantity = trade.Quantity
                };

                // remove the spent commission from the buy balance if taken from the same asset
                if (trade.IsBuyer && trade.CommissionAsset == symbol.BaseAsset)
                {
                    map.RemainingExecutedQuantity -= trade.Commission;
                }

                // log the commission event regardless
                commissions.Add(new CommissionEvent(
                    symbol,
                    trade.Time,
                    trade.OrderId,
                    trade.Id,
                    trade.CommissionAsset,
                    trade.Commission));

                mapping.Add(map);

                quantity += trade.Quantity;
            }

            if (quantity != order.ExecutedQuantity)
            {
                // we have missing trades if this happened
                LogCouldNotMatchOrder(TypeName, symbol.Name, order.Type, order.Side, order.OrderId, order.Time, order.ExecutedQuantity, quantity);

                // we cannot let algos continue work if we are missing trades or they will make incorrect decisions
                throw new AutoPositionResolverException($"Could not find all trades for {symbol.Name} {order.Type} {order.Side} order {order.OrderId} with quantity {order.ExecutedQuantity} at {order.Time}");
            }
        }

        return (mapping, commissions.ToImmutable());
    }

    private sealed class MapComparer : IComparer<Map>
    {
        private MapComparer()
        {
        }

        public int Compare(Map? x, Map? y)
        {
            if (x is null) throw new ArgumentNullException(nameof(x));
            if (y is null) throw new ArgumentNullException(nameof(y));

            return Comparer<long>.Default.Compare(x.Trade.Id, y.Trade.Id);
        }

        public static MapComparer Instance { get; } = new MapComparer();
    }

    #region Logging

    [LoggerMessage(0, LogLevel.Warning, "{TypeName} {Symbol} could not fill {OrderType} {OrderSide} order {OrderId} as there is {Missing} {Asset} missing")]
    private partial void LogCouldNotFillOrder(string typeName, string symbol, OrderType orderType, OrderSide orderSide, long orderId, decimal missing, string asset);

    [LoggerMessage(1, LogLevel.Warning, "{TypeName} {Symbol} identified {Count} positions in {ElapsedMs}ms")]
    private partial void LogIdentifiedPositions(string typeName, string symbol, int count, long elapsedMs);

    [LoggerMessage(2, LogLevel.Error, "{TypeName} {Symbol} could not match {OrderType} {OrderSide} {OrderId} at {Time} for {ExecutedQuantity} units with total trade quantity of {TradeQuantity}")]
    private partial void LogCouldNotMatchOrder(string typeName, string symbol, OrderType orderType, OrderSide orderSide, long orderId, DateTime time, decimal executedQuantity, decimal tradeQuantity);

    #endregion Logging
}