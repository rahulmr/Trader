﻿using Orleans;
using Outcompute.Trader.Trading.Algorithms;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Outcompute.Trader.Trading.ProfitAggregator
{
    // todo: implement local aggregator pattern
    internal class ProfitAggregatorGrain : Grain, IProfitAggregatorGrain
    {
        private readonly Dictionary<string, Profit> _profits = new();

        public Task PublishAsync(IEnumerable<Profit> profits)
        {
            if (profits is null) throw new ArgumentNullException(nameof(profits));

            foreach (var profit in profits)
            {
                _profits[profit.Symbol] = profit;
            }

            return Task.CompletedTask;
        }

        public Task<IEnumerable<Profit>> GetProfitsAsync()
        {
            var builder = ImmutableList.CreateBuilder<Profit>();

            foreach (var item in _profits)
            {
                builder.Add(item.Value);
            }

            return Task.FromResult<IEnumerable<Profit>>(builder.ToImmutable());
        }
    }
}