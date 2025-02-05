﻿using Outcompute.Trader.Trading.Algorithms;
using Outcompute.Trader.Trading.Algorithms.Samples.Discovery;

namespace Microsoft.Extensions.DependencyInjection;

public static class DiscoveryAlgoServiceCollectionExtensions
{
    internal const string AlgoTypeName = "Discovery";

    public static IServiceCollection AddDiscoveryAlgoType(this IServiceCollection services)
    {
        return services
            .AddAlgoType<DiscoveryAlgo>(AlgoTypeName)
            .AddOptionsType<DiscoveryAlgoOptions>()
            .Services;
    }

    public static IAlgoBuilder<IAlgo, DiscoveryAlgoOptions> AddDiscoveryAlgo(this IServiceCollection services, string name)
    {
        return services.AddAlgo<IAlgo, DiscoveryAlgoOptions>(name, AlgoTypeName);
    }
}