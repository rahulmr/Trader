﻿using Microsoft.Extensions.DependencyInjection;
using Outcompute.Trader.Models;
using Outcompute.Trader.Trading.Providers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Outcompute.Trader.Trading.Algorithms
{
    public static class ISymbolProviderAlgoContextExtensions
    {
        public static ValueTask<Symbol> GetRequiredSymbolAsync(this IAlgoContext context, string symbol, CancellationToken cancellationToken = default)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            var provider = context.ServiceProvider.GetRequiredService<ISymbolProvider>();

            return provider.GetRequiredSymbolAsync(symbol, cancellationToken);
        }
    }
}