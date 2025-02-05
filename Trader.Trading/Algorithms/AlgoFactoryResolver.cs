﻿using Orleans.Runtime;

namespace Outcompute.Trader.Trading.Algorithms;

internal class AlgoFactoryResolver : IAlgoFactoryResolver
{
    private readonly IServiceProvider _provider;

    public AlgoFactoryResolver(IServiceProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public IAlgoFactory Resolve(string typeName)
    {
        if (IsNullOrWhiteSpace(typeName)) throw new ArgumentNullException(nameof(typeName));

        return _provider.GetRequiredServiceByName<IAlgoFactory>(typeName);
    }
}