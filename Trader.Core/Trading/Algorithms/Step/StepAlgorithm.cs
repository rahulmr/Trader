﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Trader.Core.Time;

namespace Trader.Core.Trading.Algorithms.Step
{
    internal class StepAlgorithm : IStepAlgorithm
    {
        private readonly string _name;

        private readonly ILogger _logger;
        private readonly StepAlgorithmOptions _options;

        private readonly ISystemClock _clock;
        private readonly ITradingService _trader;

        public StepAlgorithm(string name, ILogger<StepAlgorithm> logger, IOptionsSnapshot<StepAlgorithmOptions> options, ISystemClock clock, ITradingService trader)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options.Get(_name) ?? throw new ArgumentNullException(nameof(options));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _trader = trader ?? throw new ArgumentNullException(nameof(trader));
        }

        private static string Type => nameof(StepAlgorithm);

        public string Symbol => _options.Symbol;

        private readonly CancellationTokenSource _cancellation = new();

        /// <summary>
        /// Set of trades synced from the trading service.
        /// </summary>
        private readonly SortedSet<AccountTrade> _trades = new(new AccountTradeIdComparer());

        /// <summary>
        /// Set of trades that compose the current asset balance.
        /// </summary>
        private readonly SortedSet<AccountTrade> _significant = new(new AccountTradeIdComparer());

        /// <summary>
        /// Descending set of open orders synced from the trading service.
        /// </summary>
        private readonly SortedSet<OrderQueryResult> _orders = new(new OrderQueryResultOrderIdComparer(false));

        private readonly SortedSet<Band> _bands = new();

        private Balances SyncAccountInfo(AccountInfo accountInfo)
        {
            _logger.LogInformation("{Type} {Name} querying account information...", Type, _name);

            var gotAsset = false;
            var gotQuote = false;

            var balances = new Balances();

            foreach (var balance in accountInfo.Balances)
            {
                if (balance.Asset == _options.Asset)
                {
                    balances.Asset.Free = balance.Free;
                    balances.Asset.Locked = balance.Locked;
                    gotAsset = true;

                    _logger.LogInformation(
                        "{Type} {Name} reports balance for base asset {Asset} is (Free = {Free}, Locked = {Locked}, Total = {Total})",
                        Type, _name, _options.Asset, balance.Free, balance.Locked, balance.Free + balance.Locked);
                }
                else if (balance.Asset == _options.Quote)
                {
                    balances.Quote.Free = balance.Free;
                    balances.Quote.Locked = balance.Locked;
                    gotQuote = true;

                    _logger.LogInformation(
                        "{Type} {Name} reports balance for quote asset {Asset} is (Free = {Free}, Locked = {Locked}, Total = {Total})",
                        Type, _name, _options.Quote, balance.Free, balance.Locked, balance.Free + balance.Locked);
                }
            }

            if (!gotAsset)
            {
                throw new AlgorithmException($"Could not get balance for base asset {_options.Asset}");
            }

            if (!gotQuote)
            {
                throw new AlgorithmException($"Could not get balance for quote asset {_options.Quote}");
            }

            return balances;
        }

        private async Task SyncAccountTradesAsync()
        {
            var trades = await _trader.GetAccountTradesAsync(new GetAccountTrades(_options.Symbol, null, null, _trades.Max?.Id + 1, 1000, null, _clock.UtcNow), _cancellation.Token);

            if (trades.Count > 0)
            {
                _trades.UnionWith(trades);

                _logger.LogInformation(
                    "{Type} {Name} got {Count} new trades from the exchange for a local total of {Total}",
                    Type, _name, trades.Count, _trades.Count);

                // remove redundant orders - this can happen when orders execute between api calls to orders and trades
                foreach (var trade in _trades)
                {
                    var removed = _orders.RemoveWhere(x => x.OrderId == trade.OrderId);
                    if (removed > 0)
                    {
                        _logger.LogWarning(
                            "{Type} {Name} removed {Count} redundant orders",
                            Type, _name, removed);
                    }
                }
            }
        }

        private async Task SyncAccountOpenOrdersAsync()
        {
            var orders = await _trader.GetOpenOrdersAsync(new GetOpenOrders(_options.Symbol, null, _clock.UtcNow));

            _orders.Clear();

            if (orders.Count > 0)
            {
                _orders.UnionWith(orders);

                _logger.LogInformation(
                    "{Type} {Name} got {Count} open orders from the exchange",
                    Type, _name, orders.Count);
            }
        }

        private async Task<SymbolPriceTicker> SyncAssetPriceAsync()
        {
            var ticker = await _trader.GetSymbolPriceTickerAsync(_options.Symbol, _cancellation.Token);

            _logger.LogInformation(
                "{Type} {Name} reports latest asset price is {Price} {QuoteAsset}",
                Type, _name, ticker.Price, _options.Quote);

            return ticker;
        }

        public async Task GoAsync(ExchangeInfo exchangeInfo, AccountInfo accountInfo, CancellationToken cancellationToken = default)
        {
            var symbol = exchangeInfo.Symbols.Single(x => x.Name == _options.Symbol);
            var priceFilter = symbol.Filters.OfType<PriceSymbolFilter>().Single();
            var lotSizeFilter = symbol.Filters.OfType<LotSizeSymbolFilter>().Single();
            var minNotionalFilter = symbol.Filters.OfType<MinNotionalSymbolFilter>().Single();

            var balances = SyncAccountInfo(accountInfo);
            await SyncAccountOpenOrdersAsync();
            await SyncAccountTradesAsync();

            // always update the latest price
            var ticker = await SyncAssetPriceAsync();

            if (TryIdentifySignificantTrades(balances)) return;
            if (TryCreateTradingBands(priceFilter, minNotionalFilter)) return;
            if (await TrySetStartingTradeAsync(symbol, ticker, priceFilter, lotSizeFilter, balances)) return;
            if (await TryCancelRogueSellOrdersAsync()) return;
            if (await TrySetBandSellOrdersAsync(balances)) return;
            if (await TryCreateLowerBandOrderAsync(symbol, ticker, priceFilter, lotSizeFilter, balances)) return;
            if (await TryCloseOutOfRangeBandsAsync(ticker, priceFilter)) return;
        }

        private async Task<bool> TryCloseOutOfRangeBandsAsync(SymbolPriceTicker ticker, PriceSymbolFilter priceFilter)
        {
            // take the lower two bands
            var bands = _bands.OrderBy(x => x.OpenPrice).Take(2).ToList();

            // if there are not at least two bands then skip this step
            if (bands.Count < 2) return false;

            // if the ticker is above the upper band then close the lower band
            if (ticker.Price > bands[1].OpenPrice && bands[0].Status == BandStatus.Ordered)
            {
                foreach (var orderId in bands[0].OpenOrderIds)
                {
                    var result = await _trader.CancelOrderAsync(new CancelStandardOrder(_options.Symbol, orderId, null, null, null, _clock.UtcNow));

                    _logger.LogInformation(
                        "{Type} {Name} closed out-of-range {OrderSide} {OrderType} for {Quantity} {Asset} at {Price} {Quote}",
                        Type, _name, result.Side, result.Type, result.OriginalQuantity, _options.Asset, result.Price, _options.Quote);
                }

                return true;
            }

            return false;
        }

        private async Task<bool> TryCreateLowerBandOrderAsync(Symbol symbol, SymbolPriceTicker ticker, PriceSymbolFilter priceFilter, LotSizeSymbolFilter lotSizeFilter, Balances balances)
        {
            // identify the highest and lowest bands
            var highBand = _bands.Max;
            var lowBand = _bands.Min;

            if (lowBand is null || highBand is null)
            {
                _logger.LogError(
                    "{Type} {Name} attempted to create a new lower band without an existing band yet",
                    Type, _name);

                // something went wrong so let the algo reset
                return true;
            }

            // skip if the current price is at or above the band open price
            if (ticker.Price >= lowBand.OpenPrice)
            {
                _logger.LogInformation(
                    "{Type} {Name} reports price {Price} {Quote} is within the current low band of {OpenPrice} {Quote} to {ClosePrice} {Quote}",
                    Type, _name, ticker.Price, _options.Quote, lowBand.OpenPrice, _options.Quote, lowBand.ClosePrice, _options.Quote);

                // let the algo continue
                return false;
            }

            // skip if we are already at the maximum number of bands
            /*
            if (_bands.Count >= _options.MaxBands)
            {
                _logger.LogWarning(
                    "{Type} {Name} has reached the maximum number of {Count} bands",
                    Type, _name, _options.MaxBands);

                // let the algo continue
                return false;
            }
            */

            // find the lower price under the current price and low band
            var lowerPrice = highBand.OpenPrice;
            var stepPrice = lowerPrice * _options.TargetPullbackRatio;
            while (lowerPrice > ticker.Price && lowerPrice > lowBand.OpenPrice)
            {
                lowerPrice -= stepPrice;
            }

            // protect some weird stuff
            if (lowerPrice <= 0)
            {
                throw new AlgorithmException($"Somehow we got to a negative lower price of {lowerPrice}!");
            }

            // under adjust the buy price to the tick size
            lowerPrice = Math.Floor(lowerPrice / priceFilter.TickSize) * priceFilter.TickSize;

            // calculate the amount to pay with
            var total = Math.Round(Math.Max(balances.Quote.Free * _options.TargetQuoteBalanceFractionPerBand, _options.MinQuoteAssetQuantityPerOrder), symbol.QuoteAssetPrecision);

            // ensure there is enough quote asset for it
            if (total > balances.Quote.Free)
            {
                _logger.LogWarning(
                    "{Type} {Name} cannot create order with amount of {Total} {Quote} because the free amount is only {Free} {Quote}",
                    Type, _name, total, _options.Quote, balances.Quote.Free, _options.Quote);

                // there's no money for creating bands so let algo continue
                return false;
            }

            // calculate the appropriate quantity to buy
            var quantity = total / lowerPrice;

            // round it down to the lot size step
            quantity = Math.Floor(quantity / lotSizeFilter.StepSize) * lotSizeFilter.StepSize;

            // place the buy order
            var result = await _trader.CreateOrderAsync(new Order(_options.Symbol, OrderSide.Buy, OrderType.Limit, TimeInForce.GoodTillCanceled, quantity, null, lowerPrice, null, null, null, NewOrderResponseType.Full, null, _clock.UtcNow), _cancellation.Token);

            _logger.LogInformation(
                "{Type} {Name} placed {OrderType} {OrderSide} for {Quantity} {Asset} at {Price} {Quote}",
                Type, _name, result.Type, result.Side, result.OriginalQuantity, _options.Asset, result.Price, _options.Quote);

            return false;
        }

        /// <summary>
        /// Sets sell orders for open bands that do not have them yet.
        /// </summary>
        private async Task<bool> TrySetBandSellOrdersAsync(Balances balances)
        {
            foreach (var band in _bands.Where(x => x.Status == BandStatus.Open))
            {
                if (band.CloseOrderId is 0)
                {
                    // acount for leftovers
                    if (band.Quantity > balances.Asset.Free)
                    {
                        _logger.LogError(
                            "{Type} {Name} cannot set band sell order of {Quantity} {Asset} for {Price} {Quote} because there are only {Balance} {Asset} free",
                            Type, _name, band.Quantity, _options.Asset, band.ClosePrice, _options.Quote, balances.Asset.Free, _options.Asset);

                        return false;
                    }

                    var result = await _trader.CreateOrderAsync(new Order(_options.Symbol, OrderSide.Sell, OrderType.Limit, TimeInForce.GoodTillCanceled, band.Quantity, null, band.ClosePrice, null, null, null, NewOrderResponseType.Full, null, _clock.UtcNow));

                    band.CloseOrderId = result.OrderId;

                    _logger.LogInformation(
                        "{Type} {Name} placed {OrderType} {OrderSide} order for band of {Quantity} {Asset} with {OpenPrice} {Quote} at {ClosePrice} {Quote}",
                        Type, _name, result.Type, result.Side, result.OriginalQuantity, _options.Asset, band.OpenPrice, _options.Quote, result.Price, _options.Quote);

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Identify and cancel rogue sell orders that do not belong to a trading band.
        /// </summary>
        private async Task<bool> TryCancelRogueSellOrdersAsync()
        {
            var fail = false;

            foreach (var order in _orders.Where(x => x.Side == OrderSide.Sell))
            {
                if (!_bands.Any(x => x.CloseOrderId == order.OrderId))
                {
                    // close the rogue sell order
                    var result = await _trader.CancelOrderAsync(new CancelStandardOrder(_options.Symbol, order.OrderId, null, null, null, _clock.UtcNow));

                    _logger.LogWarning(
                        "{Type} {Name} cancelled sell order not associated with a band for {Quantity} {Asset} at {Price} {Quote}",
                        Type, _name, result.OriginalQuantity, _options.Asset, result.Price, _options.Quote);

                    fail = true;
                }
            }

            return fail;
        }

        private async Task<bool> TrySetStartingTradeAsync(Symbol symbol, SymbolPriceTicker ticker, PriceSymbolFilter priceFilter, LotSizeSymbolFilter lotSizeFilter, Balances balances)
        {
            // only manage the opening if there are no bands or only a single order band to move around
            if (_bands.Count == 0 || (_bands.Count == 1 && _bands.Single().Status == BandStatus.Ordered))
            {
                // cancel any rogue open sell orders - this should not be possible given balance is zero at this point
                /*
                foreach (var order in _orders)
                {
                    if (order.Side is OrderSide.Sell)
                    {
                        var cancelled = await _trader.CancelOrderAsync(new CancelStandardOrder(_options.Symbol, order.OrderId, null, null, null, _clock.UtcNow), _cancellation.Token);

                        _logger.LogWarning(
                            "{Type} {Name} cancelled rogue sell order at price {Price} for {Quantity} units",
                            Type, _name, cancelled.Price, cancelled.OriginalQuantity);

                        // skip the rest of this tick to let the algo resync
                        return true;
                    }
                }
                */

                /*
                // calculate the amount to pay with
                var total = Math.Round(Math.Max(_balances.Quote.Free * _options.TargetQuoteBalanceFractionPerBand, _options.MinQuoteAssetQuantityPerOrder), _parameters.Symbol.QuoteAssetPrecision);

                // adjust to price tick size
                total = Math.Floor(total / _parameters.PriceFilter.TickSize) * _parameters.PriceFilter.TickSize;

                // ensure there is enough quote asset for it
                if (total > _balances.Quote.Free)
                {
                    _logger.LogWarning(
                        "{Type} {Name} cannot create order with amount of {Total} {Quote} because the free amount is only {Free} {Quote}",
                        Type, _name, total, _options.Quote, _balances.Quote.Free, _options.Quote);

                    return false;
                }

                var result = await _trader.CreateOrderAsync(new Order(
                    _options.Symbol,
                    OrderSide.Buy,
                    OrderType.Market,
                    null,
                    null,
                    total,
                    null,
                    null,
                    null,
                    null,
                    NewOrderResponseType.Full,
                    null,
                    _clock.UtcNow),
                    _cancellation.Token);

                _logger.LogInformation(
                    "{Type} {Name} created {OrderSide} {OrderType} order on symbol {Symbol} for {Quantity} {Asset} at price {Price} {Quote} for a total of {Total} {Quote}",
                    Type, _name, result.Side, result.Type, result.Symbol, result.OriginalQuantity, _options.Asset, result.Price, _options.Quote, result.OriginalQuantity * result.Price, _options.Quote);

                return true;
                */

                // identify the target low price for the first buy
                var lowBuyPrice = ticker.Price * (1m - _options.TargetPullbackRatio);

                // under adjust the buy price to the tick size
                lowBuyPrice = Math.Floor(lowBuyPrice / priceFilter.TickSize) * priceFilter.TickSize;

                _logger.LogInformation(
                    "{Type} {Name} identified first buy target price at {LowPrice} {LowQuote} with current price at {CurrentPrice} {CurrentQuote}",
                    Type, _name, lowBuyPrice, _options.Quote, ticker.Price, _options.Quote);

                // cancel all open buy orders with a open price lower than the lower band to the current price
                foreach (var order in _orders.Where(x => x.Side == OrderSide.Buy))
                {
                    if (order.Price < lowBuyPrice)
                    {
                        var result = await _trader.CancelOrderAsync(new CancelStandardOrder(_options.Symbol, order.OrderId, null, null, null, _clock.UtcNow));

                        _logger.LogInformation(
                            "{Type} {Name} cancelled low starting open order with price {Price} for {Quantity} units",
                            Type, _name, result.Price, result.OriginalQuantity);

                        _orders.Remove(order);

                        break;
                    }
                    else
                    {
                        _logger.LogInformation(
                            "{Type} {Name} identified a closer opening order for {Quantity} {Asset} at {Price} {Quote} and will leave as-is",
                            Type, _name, order.OriginalQuantity, _options.Asset, order.Price, _options.Quote);

                        return true;
                    }
                }

                // if there are still orders left then leave them be till the next tick
                if (_orders.Count > 0)
                {
                    return true;
                }
                else
                {
                    // put the starting order through

                    // calculate the amount to pay with
                    var total = Math.Round(Math.Max(balances.Quote.Free * _options.TargetQuoteBalanceFractionPerBand, _options.MinQuoteAssetQuantityPerOrder), symbol.QuoteAssetPrecision);

                    // ensure there is enough quote asset for it
                    if (total > balances.Quote.Free)
                    {
                        _logger.LogWarning(
                            "{Type} {Name} cannot create order with amount of {Total} {Quote} because the free amount is only {Free} {Quote}",
                            Type, _name, total, _options.Quote, balances.Quote.Free, _options.Quote);

                        return false;
                    }

                    // calculate the appropriate quantity to buy
                    var quantity = total / lowBuyPrice;

                    // round it down to the lot size step
                    quantity = Math.Floor(quantity / lotSizeFilter.StepSize) * lotSizeFilter.StepSize;

                    var order = await _trader.CreateOrderAsync(new Order(
                        _options.Symbol,
                        OrderSide.Buy,
                        OrderType.Limit,
                        TimeInForce.GoodTillCanceled,
                        quantity,
                        null,
                        lowBuyPrice,
                        null,
                        null,
                        null,
                        NewOrderResponseType.Full,
                        null,
                        _clock.UtcNow),
                        _cancellation.Token);

                    _logger.LogInformation(
                        "{Type} {Name} created {OrderSide} {OrderType} order on symbol {Symbol} for {Quantity} {Asset} at price {Price} {Quote} for a total of {Total} {Quote}",
                        Type, _name, order.Side, order.Type, order.Symbol, order.OriginalQuantity, _options.Asset, order.Price, _options.Quote, order.OriginalQuantity * order.Price, _options.Quote);

                    // skip the rest of this tick to let the algo resync
                    return true;
                }
            }
            else
            {
                return false;
            }
        }

        private bool TryIdentifySignificantTrades(Balances balances)
        {
            _significant.Clear();

            // go through all trades in lifo order
            var balance = balances.Asset.Total;
            var remaining = new Dictionary<long, decimal>();
            foreach (var trade in _trades.OrderByDescending(x => x.Time).ThenByDescending(x => x.Id))
            {
                // affect the balance
                if (trade.IsBuyer)
                {
                    // remove the buy trade from the balance to bring it close to zero
                    balance -= trade.Quantity;
                }
                else
                {
                    // add the sale trade to the balance to move it away from zero
                    balance += trade.Quantity;
                }

                // keep as a significant trade
                _significant.Add(trade);

                // keep track of the remaining quantity
                remaining[trade.Id] = trade.Quantity;

                // see if the balance zeroed out now
                if (balance is 0m) break;
            }

            if (balance is not 0)
            {
                _logger.LogWarning(
                    "{Type} {Name} has found {Balance} {Asset} unaccounted for when identifying significant trades",
                    Type, _name, balance, _options.Asset);
            }

            // now prune the significant trades to account interim sales
            var subjects = _significant.OrderBy(x => x.Time).ThenBy(x => x.Id).ToList();

            for (var i = 0; i < subjects.Count; ++i)
            {
                // loop through sales forward
                var sell = subjects[i];
                if (!sell.IsBuyer)
                {
                    // loop through buys in lifo order to find the matching buy
                    for (var j = i - 1; j >= 0; --j)
                    {
                        var buy = subjects[j];
                        if (buy.IsBuyer)
                        {
                            // remove as much as possible from the buy to satisfy the sell
                            var take = Math.Min(remaining[buy.Id], remaining[sell.Id]);
                            remaining[buy.Id] -= take;
                            remaining[sell.Id] -= take;
                        }
                    }
                }
            }

            // keep only buys with some quantity left
            _significant.Clear();
            foreach (var subject in subjects)
            {
                var quantity = remaining[subject.Id];
                if (subject.IsBuyer && quantity > 0)
                {
                    _significant.Add(new AccountTrade(subject.Symbol, subject.Id, subject.OrderId, subject.OrderListId, subject.Price, quantity, subject.QuoteQuantity, subject.Commission, subject.CommissionAsset, subject.Time, subject.IsBuyer, subject.IsMaker, subject.IsBestMatch));
                }
            }

            _logger.LogInformation(
                "{Type} {Name} identified {Count} significant trades that make up the asset balance of {Total}",
                Type, _name, _significant.Count, balances.Asset.Total);

            return false;
        }

        private bool TryCreateTradingBands(PriceSymbolFilter priceFilter, MinNotionalSymbolFilter minNotionalFilter)
        {
            _bands.Clear();

            // apply the significant buy trades to the bands
            foreach (var group in _significant.Where(x => x.IsBuyer).GroupBy(x => x.OrderId))
            {
                var band = new Band
                {
                    Quantity = group.Sum(x => x.Quantity),
                    OpenPrice = group.Sum(x => x.Price * x.Quantity) / group.Sum(x => x.Quantity),
                    Status = BandStatus.Open
                };

                band.OpenOrderIds.Add(group.Key);

                _bands.Add(band);
            }

            // apply the significant buy orders to the bands
            foreach (var order in _orders.Where(x => x.Side == OrderSide.Buy))
            {
                if (!_bands.Any(x => x.OpenOrderIds.Contains(order.OrderId)))
                {
                    var band = new Band
                    {
                        Quantity = order.OriginalQuantity,
                        OpenPrice = order.Price,
                        Status = BandStatus.Ordered
                    };

                    band.OpenOrderIds.Add(order.OrderId);

                    _bands.Add(band);
                }
            }

            // skip if no bands were created
            if (_bands.Count == 0) return false;

            // figure out the constant step size
            var stepSize = _bands.Max!.OpenPrice * _options.TargetPullbackRatio;

            // adjust close prices on the bands
            foreach (var band in _bands)
            {
                band.ClosePrice = band.OpenPrice + stepSize;
                band.ClosePrice = Math.Floor(band.ClosePrice / priceFilter.TickSize) * priceFilter.TickSize;
            }

            // apply open sell orders to the bands
            var used = new HashSet<Band>();
            foreach (var order in _orders.Where(x => x.Side == OrderSide.Sell))
            {
                var band = _bands.Except(used).FirstOrDefault(x => x.Quantity == order.OriginalQuantity && x.ClosePrice == order.Price);
                if (band is not null)
                {
                    band.CloseOrderId = order.OrderId;
                    used.Add(band);
                }
            }

            // identify bands where the target sell is somehow below the notional filter
            foreach (var band in _bands.Where(x => x.Status == BandStatus.Open && x.Quantity * x.ClosePrice < minNotionalFilter.MinNotional).ToList())
            {
                _logger.LogWarning(
                    "{Type} {Name} ignoring under notional band of {Quantity} {Asset} opening at {OpenPrice} {Quote} and closing at {ClosePrice} {Quote}",
                    Type, _name, band.Quantity, _options.Asset, band.OpenPrice, _options.Quote, band.ClosePrice, _options.Quote);

                _bands.Remove(band);
            }

            _logger.LogInformation(
                "{Type} {Name} is managing {Count} bands",
                Type, _name, _bands.Count, _bands);

            // always let the algo continue
            return false;
        }

        public IEnumerable<AccountTrade> GetTrades()
        {
            return _trades.ToImmutableList();
        }

        #region Classes

        private class SignificantTracker
        {
            public decimal RemainingQuantity { get; set; }
        }

        private enum BandStatus
        {
            Ordered,
            Open
        }

        private class Band : IComparable<Band>
        {
            public Guid Id { get; } = Guid.NewGuid();
            public HashSet<long> OpenOrderIds { get; } = new HashSet<long>();
            public decimal Quantity { get; set; }
            public decimal OpenPrice { get; set; }
            public BandStatus Status { get; set; }

            public long CloseOrderId { get; set; }

            public decimal ClosePrice { get; set; }

            public int CompareTo(Band? other)
            {
                _ = other ?? throw new ArgumentNullException(nameof(other));

                var byOpenPrice = OpenPrice.CompareTo(other.OpenPrice);
                if (byOpenPrice is not 0) return byOpenPrice;

                var byId = Id.CompareTo(other.Id);
                if (byId is not 0) return byId;

                return 0;
            }
        }

        private class Balance
        {
            public decimal Free { get; set; }
            public decimal Locked { get; set; }
            public decimal Total => Free + Locked;
        }

        private class Balances
        {
            public Balance Asset { get; } = new();
            public Balance Quote { get; } = new();
        }

        #endregion Classes
    }
}