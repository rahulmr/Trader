﻿using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Outcompute.Trader.Models;
using Outcompute.Trader.Trading.Algorithms.Positions;
using System;
using System.Collections.Immutable;

namespace Trader.Trading.Indicators.Benchmarks
{
    [MemoryDiagnoser]
    public class SignificantOrderResolverBenchmarks
    {
        private readonly IAutoPositionResolver _resolver;

        private readonly Symbol _symbol;

        private readonly DateTime _startTime;

        private readonly ImmutableSortedSet<OrderQueryResult> _orders = ImmutableSortedSet<OrderQueryResult>.Empty;

        private readonly ImmutableSortedSet<AccountTrade> _trades = ImmutableSortedSet<AccountTrade>.Empty;

        public SignificantOrderResolverBenchmarks()
        {
            var provider = new ServiceCollection()
                .AddSingleton<IAutoPositionResolver, AutoPositionResolver>()
                .AddLogging()
                .AddSqlTradingRepository(options =>
                {
                    options.ConnectionString = "server=(localdb)\\mssqllocaldb;database=trader";
                })
                .AddTraderCoreServices()
                .AddModelServices()
                .BuildServiceProvider();

            _resolver = provider.GetRequiredService<IAutoPositionResolver>();

            _symbol = Symbol.Empty with
            {
                Name = "DOGEBTC",
                BaseAsset = "DOGE",
                QuoteAsset = "BTC"
            };

            _startTime = DateTime.MinValue;
        }

        [Benchmark]
        public AutoPosition Resolve()
        {
            return _resolver.Resolve(_symbol, _orders, _trades, _startTime);
        }
    }
}