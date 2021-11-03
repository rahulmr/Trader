﻿using Microsoft.Extensions.Options;
using System.Linq;

namespace Outcompute.Trader.Trading.Algorithms
{
    internal class AlgoDependencyOptionsConfigurator : IConfigureOptions<AlgoDependencyOptions>
    {
        private readonly IOptionsMonitor<TraderOptions> _monitor;

        public AlgoDependencyOptionsConfigurator(IOptionsMonitor<TraderOptions> monitor)
        {
            _monitor = monitor;
        }

        public void Configure(AlgoDependencyOptions options)
        {
            var algos = _monitor.CurrentValue;

            // configure tickers
            options.Tickers.UnionWith(algos.Algos
                .SelectMany(x => x.Value.DependsOn.Tickers));

            // configure klines
            foreach (var algo in algos.Algos
                .SelectMany(x => x.Value.DependsOn.Klines)
                .GroupBy(x => (x.Symbol, x.Interval)))
            {
                options.Klines[(algo.Key.Symbol, algo.Key.Interval)] = algo.Max(x => x.Periods);
            }

            // configure symbols
            options.Symbols.UnionWith(algos.Algos
                .Select(x => x.Value.Symbol)
                .Where(x => !string.IsNullOrWhiteSpace(x)));

            options.Symbols.UnionWith(algos.Algos
                .SelectMany(x => x.Value.DependsOn.Balances)
                .Where(x => !string.IsNullOrWhiteSpace(x)));

            options.Symbols.UnionWith(algos.Algos
                .SelectMany(x => x.Value.DependsOn.Tickers));
        }
    }
}