﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Outcompute.Trader.Indicators;
using Outcompute.Trader.Models;
using Outcompute.Trader.Trading.Algorithms.Context;
using Outcompute.Trader.Trading.Algorithms.Positions;
using Outcompute.Trader.Trading.Commands;
using Outcompute.Trader.Trading.Indicators;
using Indicator = Outcompute.Trader.Indicators.Indicator;

namespace Outcompute.Trader.Trading.Algorithms.Samples.Oscillator
{
    internal partial class OscillatorAlgo : Algo
    {
        private readonly OscillatorAlgoOptions _options;
        private readonly ILogger _logger;

        public OscillatorAlgo(IOptionsSnapshot<OscillatorAlgoOptions> options, ILogger<OscillatorAlgo> logger)
        {
            _options = options.Get(Context.Name);
            _logger = logger;
        }

        private const string TypeName = nameof(OscillatorAlgo);
        private const string EntryBuyTag = "EntryBuy";
        private const string ExitSellTag = "ExitSell";

        protected override IAlgoCommand OnExecute()
        {
            var result = Noop();
            var lookup = new Dictionary<string, PositionStats>();

            foreach (var item in Context.Data)
            {
                try
                {
                    // skip invalidated symbol
                    if (!item.IsValid)
                    {
                        LogSkippedInvalidatedSymbol(TypeName, Context.Name, item.Symbol.Name);
                        continue;
                    }

                    // get pv stats over sellable positions from the last
                    var lots = item.AutoPosition.Positions.Reverse().EnumerateLots(item.Symbol.Filters.LotSize.StepSize).Reverse().ToList();
                    var stats = lots.GetStats(item.Ticker.ClosePrice);

                    // calculate current technical ratings for this asset
                    var source = item.Klines.ToOHLCV().Identity();
                    var ratings = Indicator.TechnicalRatings(source);

                    result = result.Then(TryEnter(item, lots, stats, source, ratings));

                    if (TryExit(item, lots, stats, source, ratings, out var exit))
                    {
                        result = result.Then(exit);
                    }

                    // cache for the reporting method
                    lookup[item.Symbol.Name] = stats;
                }
                catch (Exception ex)
                {
                    LogFailedToProcessSymbol(TypeName, Context.Name, item.Symbol.Name, ex);
                    throw;
                }
            }

            ReportAggregateStats(lookup);

            return result;
        }

        private decimal CalculateBuyQuantity(SymbolData item, PositionStats stats, decimal price)
        {
            // calculate the notional to use for buying
            var balance = item.Spot.QuoteAsset.Free + item.Savings.QuoteAsset.FreeAmount + item.SwapPools.QuoteAsset.Total;
            var notional = balance * _options.EntryNotionalRate;

            // raise to the user min notional
            notional = Math.Max(notional, _options.MinNotional);

            // top up with past realized profits for the first buy only
            if (_options.UseProfits && stats.TotalQuantity == 0)
            {
                notional += item.AutoPosition.ProfitEvents.Sum(x => x.Profit);
                notional -= item.AutoPosition.CommissionEvents.Where(x => x.Asset == item.Symbol.QuoteAsset).Sum(x => x.Commission);
            }

            // raise to a valid number
            notional = item.Symbol.RaiseTotalUpToMinNotional(notional);
            notional = item.Symbol.RaisePriceToTickSize(notional);

            // calculate the quantity for the limit order
            var quantity = notional / price;

            // raise the quantity to a valid number
            quantity = item.Symbol.RaiseQuantityToMinLotSize(quantity);
            quantity = item.Symbol.RaiseQuantityToLotStepSize(quantity);

            return quantity;
        }

        private IAlgoCommand TryEnter(SymbolData item, IList<PositionLot> lots, PositionStats stats, Identity<OHLCV> source, TechnicalRatings ratings)
        {
            IAlgoCommand Clear() => CancelOpenOrders(item.Symbol, OrderSide.Buy);

            // skip if the symbol is on buy cooldown
            if (lots.Count > 0 && lots[^1].Time.Add(_options.BuyCooldown) >= Context.TickTime)
            {
                return Clear();
            }

            // skip if opening disabled
            if (lots.Count == 0 && _options.ExcludeFromOpening.Contains(item.Symbol.Name))
            {
                LogSymbolClosed(TypeName, Context.Name, item.Symbol.Name);
                return Clear();
            }

            // calculate the atr for reuse
            var atr = Indicator.Atr(Indicator.Transform(source, x => x.ToHLC()));
            var prevAtr = atr.Count > 2 ? atr[^2] : null;
            if (!prevAtr.HasValue)
            {
                return Clear();
            }

            // ratings must be close to the entry point to bother reserving funds
            if (ratings[^1].Summary.Rating < -TechnicalRatings.WeakBound)
            {
                return Clear();
            }

            // cleanup any existing orders outside the atr
            var maxPrice = item.Symbol.LowerPriceToTickSize(item.Klines[^2].ClosePrice + prevAtr.Value);
            if (item.Orders.Open.Any(x => x.Side == OrderSide.Buy && x.StopPrice > maxPrice))
            {
                return Clear();
            }

            // the few past ratings must be negative to make a wave
            for (var i = ratings.Count - 2; i >= ratings.Count - 5 && i >= 0; i--)
            {
                if (ratings[i].Summary.Rating >= 0)
                {
                    return Clear();
                }
            }

            var window = 0.001M;
            var stopPrice = decimal.MaxValue;
            var buyPrice = decimal.MaxValue;

            if (ratings[^1].Summary.Rating < 0 && ratings.TryPredict(source, 0, Aproximate.Up, out var summary) && summary.Item.Close.HasValue)
            {
                var stop = item.Symbol.LowerPriceToTickSize(summary.Item.Close.Value);
                var price = item.Symbol.LowerPriceToTickSize(stop * (1 + window));
                var distance = item.Symbol.LowerPriceToTickSize(Math.Abs(item.Klines[^2].ClosePrice - price));

                if (item.Ticker.ClosePrice < stop && distance < prevAtr.Value / 2M)
                {
                    stopPrice = Math.Min(stopPrice, stop);
                    buyPrice = Math.Min(buyPrice, price);
                }
            }

            // ignore if we cant predict yet - the price may be jumping around the target
            if (stopPrice == decimal.MaxValue)
            {
                return Noop();
            }

            // define the quantity to buy
            var quantity = CalculateBuyQuantity(item, stats, stopPrice);

            // skip if there is already an executable open buy order open at the same stop price
            if (item.Orders.Open.Any(x => x.Side == OrderSide.Buy && x.StopPrice <= stopPrice && x.Price >= item.Ticker.ClosePrice))
            {
                return Noop();
            }

            return Sequence(
                EnsureSpotBalance(item.Symbol.QuoteAsset, item.Symbol.RaisePriceToTickSize(quantity * buyPrice), true, true),
                EnsureSingleOrder(item.Symbol, OrderSide.Buy, OrderType.StopLossLimit, TimeInForce.GoodTillCanceled, quantity, null, buyPrice, stopPrice, EntryBuyTag));
        }

        private bool TryExit(SymbolData item, IReadOnlyList<PositionLot> lots, PositionStats stats, Identity<OHLCV> source, TechnicalRatings ratings, out IAlgoCommand command)
        {
            command = Noop();

            // there must be something to sell
            if (stats.TotalQuantity == 0)
            {
                return false;
            }

            var stopPrice = 0M;
            var sellPrice = 0M;

            // this will be the sell price window
            var window = 0.01M;

            var atrp = item.Klines.SkipLast(1).ToAtr().Last();

            /*
            if (atrp.HasValue)
            {
                var chandellierOpen = lots[^1].Time;
                var chandellierHigh = item.Klines.Reverse().TakeWhile(x => x.CloseTime >= chandellierOpen).Max(x => x.HighPrice);
                var chandellierStop = item.Symbol.RaisePriceToTickSize(chandellierHigh - (atrp.Value * _options.AtrMultiplier));
                var chandellierPrice = item.Symbol.RaisePriceToTickSize(chandellierStop * (1 - window));
                if (item.Ticker.ClosePrice > chandellierStop)
                {
                    stopPrice = Math.Max(stopPrice, chandellierStop);
                    sellPrice = Math.Max(sellPrice, chandellierPrice);
                }
            }
            */

            // define a trailing take target
            var target = ratings[^1].Summary.Rating - TechnicalRatings.WeakBound;

            // guard - raise to the price that makes ratings go down to neutral
            if (ratings.TryPredict(source, target, Aproximate.Up, out var summary) && summary.Item.Close.HasValue)
            {
                var stop = item.Symbol.RaisePriceToTickSize(summary.Item.Close.Value);
                var price = item.Symbol.RaisePriceToTickSize(stop * (1 - window));

                if (item.Ticker.ClosePrice > stop)
                {
                    stopPrice = Math.Max(stopPrice, stop);
                    sellPrice = Math.Max(sellPrice, price);
                }
            }

            // calculate tracking stop for reuse
            var trailingStop = item.Symbol.RaisePriceToTickSize(item.Ticker.ClosePrice * (1 - window));
            var trailingPrice = item.Symbol.RaisePriceToTickSize(trailingStop * (1 - window));

            // take - raise to the profit target from the last entry
            var profitPrice = item.Symbol.RaisePriceToTickSize(lots[^1].AvgPrice * 1.1M);
            var profitStop = item.Symbol.RaisePriceToTickSize(profitPrice * (1 + window));
            if (item.Ticker.ClosePrice > profitStop && profitStop >= trailingStop)
            {
                sellPrice = Math.Max(sellPrice, trailingPrice);
                stopPrice = Math.Max(stopPrice, trailingStop);
            }

            if (stopPrice == 0M)
            {
                return false;
            }

            // calculate the sellable quantity
            var quantity = 0M;
            if (_options.LossEnabled)
            {
                quantity = stats.TotalQuantity;
            }
            else
            {
                if (!TryGetElectedQuantity(item, lots, sellPrice, out quantity))
                {
                    return false;
                }
            }

            // skip if there is already an executable order at a higher price for a greater quantity
            if (item.Orders.Open.Any(x => x.Side == OrderSide.Sell && x.StopPrice >= stopPrice && x.OriginalQuantity >= quantity && x.Price <= item.Ticker.ClosePrice))
            {
                return false;
            }

            // take any current sell orders into account for spot balance release
            var locked = item.Orders.Open.Where(x => x.Side == OrderSide.Sell).Sum(x => x.OriginalQuantity);
            var required = Math.Max(quantity - locked, 0);
            var notional = item.Symbol.LowerPriceToTickSize(quantity * sellPrice);

            if (quantity < item.Symbol.Filters.LotSize.MinQuantity)
            {
                LogCannotPlaceSellOrderWithQuantityUnderMin(TypeName, Context.Name, item.Symbol.Name, quantity, item.Symbol.BaseAsset, item.Symbol.Filters.LotSize.MinQuantity);
                return false;
            }

            if (notional < item.Symbol.Filters.MinNotional.MinNotional)
            {
                LogCannotPlaceSellOrderWithNotionalUnderMin(TypeName, Context.Name, item.Symbol.Name, notional, item.Symbol.QuoteAsset, item.Symbol.Filters.MinNotional.MinNotional);
                return false;
            }

            // place the exit order now
            command = Sequence(
                EnsureSpotBalance(item.Symbol.BaseAsset, required, true, true),
                EnsureSingleOrder(item.Symbol, OrderSide.Sell, OrderType.StopLossLimit, TimeInForce.GoodTillCanceled, quantity, null, sellPrice, stopPrice, ExitSellTag));

            return true;
        }

        private static bool TryGetElectedQuantity(SymbolData item, IReadOnlyList<PositionLot> lots, decimal sellPrice, out decimal electedQuantity)
        {
            // gather all the lots that fit under the sell price
            var quantity = 0M;
            var buyNotional = 0M;
            var sellNotional = 0M;

            electedQuantity = 0M;

            foreach (var lot in lots.Reverse())
            {
                // keep adding everything up so we get a average from the end
                quantity += lot.Quantity;
                buyNotional += lot.Quantity * lot.AvgPrice;
                sellNotional += lot.Quantity * sellPrice;

                // continue until the quantity is sellable
                if (quantity < item.Symbol.Filters.LotSize.MinQuantity)
                {
                    continue;
                }

                // continue until the notional is sellable
                if (sellNotional < item.Symbol.Filters.MinNotional.MinNotional)
                {
                    continue;
                }

                // the average cost price must fit under the profit price
                var avgPrice = buyNotional / quantity;
                avgPrice = item.Symbol.RaisePriceToTickSize(avgPrice);
                if (avgPrice > sellPrice)
                {
                    continue;
                }

                // keep the candidate quantity and continue looking for more
                electedQuantity = quantity;
            }

            return electedQuantity > 0;
        }

        private void ReportAggregateStats(Dictionary<string, PositionStats> lookup)
        {
            var reportable = Context.Data.Where(x => x.IsValid);

            var grouped = reportable
                .GroupBy(x => x.Symbol.QuoteAsset);

            // report on total portfolio value for each quote
            foreach (var quote in grouped)
            {
                // get stats for every sellable symbol
                var stats = quote.Select(x => (x.Symbol, Stats: lookup[x.Symbol.Name]));

                var (cost, pv, rpnl) = quote
                    .Select(x =>
                    (
                        Unrealized: x.AutoPosition.Positions.GetStats(x.Ticker.ClosePrice),
                        Realized: x.AutoPosition.ProfitEvents.Sum(x => x.Profit)
                    ))
                    .Aggregate(
                        (Cost: 0M, PV: 0M, RPNL: 0M),
                        (agg, item) => (agg.Cost + item.Unrealized.TotalCost, agg.PV + item.Unrealized.PresentValue, agg.RPNL + item.Realized));

                LogAssetInfo(
                    TypeName,
                    Context.Name,
                    quote.Key,
                    cost,
                    pv,
                    cost == 0M ? 0M : (pv - cost) / cost,
                    pv - cost,
                    rpnl,
                    pv - cost + rpnl);

                // every unsellable symbol with opening disabled
                /*
                foreach (var item in stats
                    .Where(x => x.Stats.TotalQuantity < x.Symbol.Filters.LotSize.MinQuantity)
                    .Where(x => _options.Buying.Opening.ExcludeSymbols.Contains(x.Symbol.Name)))
                {
                    LogClosedSymbol(TypeName, Context.Name, item.Symbol.Name, item.Stats.TotalQuantity, item.Symbol.BaseAsset, item.Stats.PresentValue, item.Symbol.QuoteAsset);
                }
                */

                // every non zero symbol by relative pnl
                foreach (var item in stats
                    .Where(x => x.Stats.TotalQuantity > 0)
                    .OrderBy(x => x.Stats.RelativePnL))
                {
                    LogSymbolPv(TypeName, Context.Name, item.Symbol.Name, item.Stats.RelativePnL, item.Stats.AbsolutePnL, item.Symbol.QuoteAsset, item.Stats.PresentValue);
                }

                // report on the absolute loser
                var absLoser = stats
                    .OrderBy(x => x.Stats.AbsolutePnL)
                    .FirstOrDefault();

                if (!IsNullOrEmpty(absLoser.Symbol?.Name))
                {
                    LogSymbolWithLowestAbsolutePnl(TypeName, Context.Name, absLoser.Symbol.Name, absLoser.Stats.AbsolutePnL);
                }

                // report on the relative loser
                var relLoser = stats
                    .OrderBy(x => x.Stats.RelativePnL)
                    .FirstOrDefault();

                if (!IsNullOrEmpty(relLoser.Symbol?.Name))
                {
                    LogSymbolWithLowestRelativePnl(TypeName, Context.Name, relLoser.Symbol.Name, relLoser.Stats.RelativePnL);
                }

                // report on the absolute winner
                var absWinner = stats
                    .OrderByDescending(x => x.Stats.AbsolutePnL)
                    .FirstOrDefault();

                if (!IsNullOrEmpty(absWinner.Symbol?.Name))
                {
                    LogSymbolWithHighestAbsolutePnl(TypeName, Context.Name, absWinner.Symbol.Name, absWinner.Stats.AbsolutePnL);
                }

                // report on the relative loser
                var relWinner = stats
                    .OrderByDescending(x => x.Stats.RelativePnL)
                    .FirstOrDefault();

                if (!IsNullOrEmpty(relWinner.Symbol?.Name))
                {
                    LogSymbolWithHighestRelativePnl(TypeName, Context.Name, relWinner.Symbol.Name, relWinner.Stats.RelativePnL);
                }

                // report on the highest sellable pv
                /*
                var highPv = stats
                    .Where(x => x.Stats.TotalQuantity > 0)
                    .Where(x => !_options.Selling.ExcludeSymbols.Contains(x.Symbol.Name))
                    .OrderByDescending(x => x.Stats.PresentValue)
                    .FirstOrDefault();

                if (!IsNullOrEmpty(highPv.Symbol?.Name))
                {
                    LogSymbolWithHighestPresentValue(TypeName, Context.Name, highPv.Symbol.Name, highPv.Stats.PresentValue, highPv.Symbol.QuoteAsset);
                }
                */

                // report on sellable pv
                /*
                var highPvBreakEven = stats
                    .Where(x => !_options.Selling.ExcludeSymbols.Contains(x.Symbol.Name))
                    .Where(x => x.Stats.TotalQuantity > 0)
                    .Where(x => x.Stats.RelativePnL >= 0)
                    .OrderByDescending(x => x.Stats.PresentValue)
                    .FirstOrDefault();

                if (!IsNullOrEmpty(highPvBreakEven.Symbol?.Name))
                {
                    LogSymbolWithHighestPresentValueAboveBreakEven(TypeName, Context.Name, highPvBreakEven.Symbol.Name, highPvBreakEven.Stats.PresentValue, highPvBreakEven.Symbol.QuoteAsset);
                }
                */
            }
        }

        #region Logging

        [LoggerMessage(1, LogLevel.Error, "{Type} {Name} skipped invalidated symbol {Symbol}")]
        private partial void LogSkippedInvalidatedSymbol(string type, string name, string symbol);

        [LoggerMessage(2, LogLevel.Information, "{Type} {Name} {Symbol} entry skipped symbol on cooldown until {Cooldown}")]
        private partial void LogEntrySkippedSymbolOnCooldown(string type, string name, string symbol, DateTime cooldown);

        [LoggerMessage(3, LogLevel.Information, "{Type} {Name} {Symbol} entry skipped symbol with low current RMI of {RMI:F2}")]
        private partial void LogEntrySkippedSymbolWithLowCurrentRmi(string type, string name, string symbol, decimal rmi);

        [LoggerMessage(4, LogLevel.Information, "{Type} {Name} {Symbol} entry skipped symbol with high previous RMI of {RMI:F2}")]
        private partial void LogEntrySkippedSymbolWithHighPrevRmi(string type, string name, string symbol, decimal rmi);

        [LoggerMessage(5, LogLevel.Information, "{Type} {Name} {Symbol} entry skipped symbol with high current RMI of {RMI:F2}")]
        private partial void LogEntrySkippedSymbolWithHighCurrentRmi(string type, string name, string symbol, decimal rmi);

        [LoggerMessage(6, LogLevel.Information, "{Type} {Name} {Symbol} entry cannot calculate price for entry RMI of {RMI:F2}")]
        private partial void LogEntrySkippedSymbolWithUnknownRmiPrice(string type, string name, string symbol, decimal rmi);

        [LoggerMessage(7, LogLevel.Information, "{Type} {Name} {Symbol} exit skipped symbol with quantity of {Quantity:F8} {Asset} under min lot size of {MinLotSize:F8} {Asset}")]
        private partial void LogExitSkippedSymbolWithQuantityUnderMinLotSize(string type, string name, string symbol, decimal quantity, decimal minLotSize, string asset);

        [LoggerMessage(8, LogLevel.Information, "{Type} {Name} {Symbol} exist skipped symbol with RMI of {RMI:F2} under high threshold of {HighRMI:F2}")]
        private partial void LogExitSkippedSymbolWithRmiUnderHighThreshold(string type, string name, string symbol, decimal rmi, decimal highRmi);

        [LoggerMessage(9, LogLevel.Information, "{Type} {Name} {Symbol} exit skipped symbol with unsellable notional {Notional:F8} {Quote} from quantity {Quantity} {Asset} at price {Price} {Quote}")]
        private partial void LogExitSkippedSymbolWithUnsellableNotional(string type, string name, string symbol, decimal notional, decimal quantity, decimal price, string asset, string quote);

        [LoggerMessage(10, LogLevel.Information, "{Type} {Name} {Symbol} exit cannot calculate price for entry RMI of {RMI:F2}")]
        private partial void LogExitSkippedSymbolWithUnknownRmiPrice(string type, string name, string symbol, decimal rmi);

        [LoggerMessage(11, LogLevel.Information, "{Type} {Name} {Symbol} exit skipped symbol without lots under the exit price of {ExitPrice:F8} {Quote}")]
        private partial void LogExitSkippedSymbolWithoutLotsUnderExitPrice(string type, string name, string symbol, decimal exitPrice, string quote);

        [LoggerMessage(12, LogLevel.Warning, "{Type} {Name} {Symbol} cannot place sell order with quantity of {Quantity:F8} {Asset} under min of {MinQuantity:F8} {Asset}")]
        private partial void LogCannotPlaceSellOrderWithQuantityUnderMin(string type, string name, string symbol, decimal quantity, string asset, decimal minQuantity);

        [LoggerMessage(13, LogLevel.Warning, "{Type} {Name} {Symbol} cannot place sell order with notional of {Notional:F8} {Quote} under min of {MinNotional:F8} {Quote}")]
        private partial void LogCannotPlaceSellOrderWithNotionalUnderMin(string type, string name, string symbol, decimal notional, string quote, decimal minNotional);

        [LoggerMessage(14, LogLevel.Information, "{Type} {Name} reports {Quote} asset info (U-Cost: {UCost:F8}, U-PV: {UPV:F8}: U-RPnL: {URPNL:P2}, U-AbsPnL: {UAPNL:F8}, R-AbsPnL: {RAPNL:F8}, T-AbsPnL:{TAPNL:F8})")]
        private partial void LogAssetInfo(string type, string name, string quote, decimal ucost, decimal upv, decimal urpnl, decimal uapnl, decimal rapnl, decimal tapnl);

        [LoggerMessage(15, LogLevel.Information, "{Type} {Name} reports symbol {Symbol} with (PnL: {UnrealizedPnl:P2}, Unrealized: {UnrealizedAbsPnl:F8} {Quote}, PV: {PV:F8} {Quote}")]
        private partial void LogSymbolPv(string type, string name, string symbol, decimal unrealizedPnl, decimal unrealizedAbsPnl, string quote, decimal pv);

        [LoggerMessage(16, LogLevel.Information, "{Type} {Name} reports symbol {Symbol} with lowest unrealized absolute pnl {UnrealizedAbsolutePnl:F8}")]
        private partial void LogSymbolWithLowestAbsolutePnl(string type, string name, string symbol, decimal unrealizedAbsolutePnl);

        [LoggerMessage(17, LogLevel.Information, "{Type} {Name} reports symbol {Symbol} with lowest unrealized relative pnl {UnrealizedRelativePnl:P2}")]
        private partial void LogSymbolWithLowestRelativePnl(string type, string name, string symbol, decimal unrealizedRelativePnl);

        [LoggerMessage(18, LogLevel.Information, "{Type} {Name} reports symbol {Symbol} with highest unrealized absolute pnl {UnrealizedAbsolutePnl:F8}")]
        private partial void LogSymbolWithHighestAbsolutePnl(string type, string name, string symbol, decimal unrealizedAbsolutePnl);

        [LoggerMessage(19, LogLevel.Information, "{Type} {Name} reports symbol {Symbol} with highest unrealized relative pnl {UnrealizedRelativePnl:P2}")]
        private partial void LogSymbolWithHighestRelativePnl(string type, string name, string symbol, decimal unrealizedRelativePnl);

        [LoggerMessage(20, LogLevel.Information, "{Type} {Name} reports symbol {Symbol} trading is now closed")]
        private partial void LogSymbolClosed(string type, string name, string symbol);

        [LoggerMessage(21, LogLevel.Information, "{Type} {Name} processing symbol {Symbol}...")]
        private partial void LogProcessingSymbol(string type, string name, string symbol);

        [LoggerMessage(22, LogLevel.Error, "{Type} {Name} failed to process symbol")]
        private partial void LogFailedToProcessSymbol(string type, string name, string symbol, Exception ex);

        #endregion Logging
    }
}