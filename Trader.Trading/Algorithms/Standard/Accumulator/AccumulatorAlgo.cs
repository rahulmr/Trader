﻿using Microsoft.Extensions.Options;
using Outcompute.Trader.Core.Time;
using Outcompute.Trader.Models;
using Outcompute.Trader.Trading.Commands;

namespace Outcompute.Trader.Trading.Algorithms.Standard.Accumulator
{
    internal class AccumulatorAlgo : Algo
    {
        private readonly IOptionsMonitor<AccumulatorAlgoOptions> _options;
        private readonly ISystemClock _clock;

        public AccumulatorAlgo(IOptionsMonitor<AccumulatorAlgoOptions> options, ISystemClock clock)
        {
            _options = options;
            _clock = clock;
        }

        protected override ValueTask<IAlgoCommand> OnExecuteAsync(CancellationToken cancellationToken = default)
        {
            // fix options for this run
            var options = _options.Get(Context.Name);

            // calculate the current rsi
            var rsi = Context.Klines.LastRsi(x => x.ClosePrice, options.RsiPeriods);

            var buyCommand = TrySignalBuy(options, rsi)
                ? TrackingBuy(1.000M, 0.001M, null, true, true)
                : ClearOpenOrders(OrderSide.Buy);

            var sellCommand = TrySignalSell(options)
                ? MarketSell(Context.Symbol, Context.PositionDetails.Orders.Sum(x => x.ExecutedQuantity), true, true)
                : Noop();

            return ValueTask.FromResult(Many(buyCommand, sellCommand));
        }

        private bool TrySignalSell(AccumulatorAlgoOptions options)
        {
            // if there is nothing to sell then skip
            if (Context.PositionDetails.Orders.Sum(x => x.ExecutedQuantity) < Context.Symbol.Filters.LotSize.MinQuantity)
            {
                return false;
            }

            // if the ticker is below the take profit drop rate then sell
            var averageCost = Context.PositionDetails.Orders.Sum(x => x.ExecutedQuantity * x.Price) / Context.PositionDetails.Orders.Sum(x => x.ExecutedQuantity);
            var takeProfitPrice = Context.PositionDetails.Orders.Max!.Price * options.TakeProfitDropRate;
            var ticker = Context.Ticker.ClosePrice;
            if (ticker <= takeProfitPrice && takeProfitPrice >= averageCost)
            {
                return true;
            }

            // otherwise carry on
            return false;
        }

        private bool TrySignalBuy(AccumulatorAlgoOptions options, decimal rsi)
        {
            // if there are no open positions or we only have leftovers then buy when the rsi hits oversold
            if (Context.PositionDetails.Orders.Sum(x => x.ExecutedQuantity) < Context.Symbol.Filters.LotSize.MinQuantity)
            {
                return rsi <= options.RsiOversold;
            }

            // if the last buy is within cooldown then refuse to buy
            if (Context.PositionDetails.Orders.Max!.Time.Add(options.Cooldown) >= _clock.UtcNow)
            {
                return false;
            }

            // if the ticker is above the next buy rate of the last buy then signal the buy
            if (Context.Ticker.ClosePrice >= Context.PositionDetails.Orders.Max!.Price * options.NextBuyRate)
            {
                return true;
            }

            // otherwise refuse to buy
            return false;
        }
    }
}