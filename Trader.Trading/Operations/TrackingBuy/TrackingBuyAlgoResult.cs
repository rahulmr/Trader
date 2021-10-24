﻿using Microsoft.Extensions.DependencyInjection;
using Outcompute.Trader.Models;
using Outcompute.Trader.Trading.Algorithms;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Outcompute.Trader.Trading.Operations.TrackingBuy
{
    public class TrackingBuyAlgoResult : IAlgoResult
    {
        public TrackingBuyAlgoResult(Symbol symbol, decimal pullbackRatio, decimal targetQuoteBalanceFractionPerBuy, decimal? maxNotional, bool redeemSavings)
        {
            Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
            PullbackRatio = pullbackRatio;
            TargetQuoteBalanceFractionPerBuy = targetQuoteBalanceFractionPerBuy;
            MaxNotional = maxNotional;
            RedeemSavings = redeemSavings;
        }

        public Symbol Symbol { get; }
        public decimal PullbackRatio { get; }
        public decimal TargetQuoteBalanceFractionPerBuy { get; }
        public decimal? MaxNotional { get; }
        public bool RedeemSavings { get; }

        public Task ExecuteAsync(IAlgoContext context, CancellationToken cancellationToken = default)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            return context.ServiceProvider
                .GetRequiredService<IAlgoResultExecutor<TrackingBuyAlgoResult>>()
                .ExecuteAsync(context, this, cancellationToken);
        }
    }
}