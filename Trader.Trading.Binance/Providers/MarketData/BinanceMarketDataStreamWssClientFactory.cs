﻿using Microsoft.Extensions.DependencyInjection;

namespace Outcompute.Trader.Trading.Binance.Providers.MarketData;

internal class BinanceMarketDataStreamWssClientFactory : IMarketDataStreamClientFactory
{
    private readonly IServiceProvider _provider;

    public BinanceMarketDataStreamWssClientFactory(IServiceProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public IMarketDataStreamClient Create(IReadOnlyCollection<string> streams)
    {
        return ActivatorUtilities.CreateInstance<BinanceMarketDataStreamWssClient>(_provider, streams);
    }
}