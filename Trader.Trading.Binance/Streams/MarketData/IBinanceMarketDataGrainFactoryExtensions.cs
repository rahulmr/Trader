﻿using Outcompute.Trader.Trading.Binance.Streams.MarketData;
using System;

namespace Orleans
{
    internal static class IBinanceMarketDataGrainFactoryExtensions
    {
        public static IBinanceMarketDataGrain GetBinanceMarketDataGrain(this IGrainFactory factory)
        {
            if (factory is null) throw new ArgumentNullException(nameof(factory));

            return factory.GetGrain<IBinanceMarketDataGrain>(Guid.Empty);
        }
    }
}