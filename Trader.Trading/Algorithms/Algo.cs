﻿using Microsoft.Extensions.DependencyInjection;
using Outcompute.Trader.Models;
using Outcompute.Trader.Trading.Operations;
using Outcompute.Trader.Trading.Operations.AveragingSell;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Outcompute.Trader.Trading.Algorithms
{
    /// <summary>
    /// Base class for algos that do not follow the suggested lifecycle.
    /// For symbol based algos, consider implementing <see cref="SymbolAlgo"/> instead.
    /// </summary>
    public abstract class Algo : IAlgo
    {
        public abstract Task<IAlgoResult> GoAsync(CancellationToken cancellationToken = default);

        public virtual Task StartAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public virtual Task StopAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public IAlgoContext Context { get; set; } = NullAlgoContext.Instance;

        public virtual IAlgoResult Noop()
        {
            return NullAlgoResult.Instance;
        }

        public virtual IAlgoResult AveragingSell(Symbol symbol, IReadOnlyCollection<OrderQueryResult> orders, decimal profitMultiplier, bool redeemSavings)
        {
            return new AveragingSellResult(symbol, orders, profitMultiplier, redeemSavings);
        }

        public virtual Task<OrderResult> CreateOrderAsync(Symbol symbol, OrderType type, OrderSide side, TimeInForce timeInForce, decimal quantity, decimal price, string? tag, CancellationToken cancellationToken = default)
        {
            return Context.ServiceProvider
                .GetRequiredService<ICreateOrderOperation>()
                .CreateOrderAsync(symbol, type, side, timeInForce, quantity, price, tag, cancellationToken);
        }

        public virtual Task<CancelStandardOrderResult> CancelOrderAsync(string symbol, long orderId, CancellationToken cancellationToken = default)
        {
            return Context.ServiceProvider
                .GetRequiredService<ICancelOrderOperation>()
                .CancelOrderAsync(symbol, orderId, cancellationToken);
        }

        public virtual Task<bool> EnsureSingleOrderAsync(Symbol symbol, OrderSide side, OrderType type, TimeInForce timeInForce, decimal quantity, decimal price, bool redeemSavings, CancellationToken cancellationToken = default)
        {
            return Context.ServiceProvider
                .GetRequiredService<IEnsureSingleOrderOperation>()
                .EnsureSingleOrderAsync(symbol, side, type, timeInForce, quantity, price, redeemSavings, cancellationToken);
        }

        public virtual Task ClearOpenOrdersAsync(Symbol symbol, OrderSide side, CancellationToken cancellationToken = default)
        {
            return Context.ServiceProvider
                .GetRequiredService<IClearOpenOrdersOperation>()
                .ClearOpenOrdersAsync(symbol, side, cancellationToken);
        }

        public virtual Task<IReadOnlyList<OrderQueryResult>> GetOpenOrdersAsync(Symbol symbol, OrderSide side, CancellationToken cancellationToken = default)
        {
            return Context.ServiceProvider
                .GetRequiredService<IGetOpenOrdersOperation>()
                .GetOpenOrdersAsync(symbol, side, cancellationToken);
        }

        public virtual Task<(bool Success, decimal Redeemed)> TryRedeemSavingsAsync(string asset, decimal amount, CancellationToken cancellationToken = default)
        {
            return Context.ServiceProvider
                .GetRequiredService<IRedeemSavingsOperation>()
                .TryRedeemSavingsAsync(asset, amount, cancellationToken);
        }

        public virtual Task SetSignificantAveragingSellAsync(Symbol symbol, MiniTicker ticker, IReadOnlyCollection<OrderQueryResult> orders, decimal minimumProfitRate, bool redeemSavings, CancellationToken cancellationToken = default)
        {
            return Context.ServiceProvider
                .GetRequiredService<ISignificantAveragingSellOperation>()
                .SetSignificantAveragingSellAsync(symbol, ticker, orders, minimumProfitRate, redeemSavings, cancellationToken);
        }

        public virtual Task<bool> SetTrackingBuyAsync(Symbol symbol, decimal pullbackRatio, decimal targetQuoteBalanceFractionPerBuy, decimal? maxNotional, bool redeemSavings, CancellationToken cancellationToken = default)
        {
            return Context.ServiceProvider
                .GetRequiredService<ITrackingBuyOperation>()
                .SetTrackingBuyAsync(symbol, pullbackRatio, targetQuoteBalanceFractionPerBuy, maxNotional, redeemSavings, cancellationToken);
        }
    }
}