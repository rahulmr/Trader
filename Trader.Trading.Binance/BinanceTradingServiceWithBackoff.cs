﻿using Polly;

namespace Outcompute.Trader.Trading.Binance;

internal partial class BinanceTradingServiceWithBackoff : ITradingService
{
    private readonly ILogger _logger;
    private readonly ITradingService _trader;

    public BinanceTradingServiceWithBackoff(ILogger<BinanceTradingServiceWithBackoff> logger, ITradingService trader)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _trader = trader ?? throw new ArgumentNullException(nameof(trader));
    }

    private const string TypeName = nameof(BinanceTradingServiceWithBackoff);

    private IAsyncPolicy CreatePolicy()
    {
        return Policy
            .Handle<BinanceTooManyRequestsException>()
            .WaitAndRetryForeverAsync(
                (n, ex, ctx) => ((BinanceTooManyRequestsException)ex).RetryAfter.Add(TimeSpan.FromSeconds(1)),
                (ex, ts, ctx) =>
                {
                    LogBackingOff(ex, TypeName, ts.Add(TimeSpan.FromSeconds(1)));
                    return Task.CompletedTask;
                });
    }

    private Task WaitAndRetryForeverAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        return CreatePolicy().ExecuteAsync(ct => action(ct), cancellationToken, false);
    }

    private Task<TResult> WaitAndRetryForeverAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken cancellationToken = default)
    {
        return CreatePolicy().ExecuteAsync(ct => action(ct), cancellationToken, false);
    }

    public ITradingService WithBackoff() => this;

    public Task<CancelStandardOrderResult> CancelOrderAsync(string symbol, long orderId, CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.CancelOrderAsync(symbol, orderId, ct), cancellationToken);
    }

    public Task CloseUserDataStreamAsync(string listenKey, CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.CloseUserDataStreamAsync(listenKey, ct), cancellationToken);
    }

    public Task<OrderResult> CreateOrderAsync(string symbol, OrderSide side, OrderType type, TimeInForce? timeInForce, decimal? quantity, decimal? quoteOrderQuantity, decimal? price, string? newClientOrderId, decimal? stopPrice, decimal? icebergQuantity, CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.CreateOrderAsync(symbol, side, type, timeInForce, quantity, quoteOrderQuantity, price, newClientOrderId, stopPrice, icebergQuantity, ct), cancellationToken);
    }

    public Task<string> CreateUserDataStreamAsync(CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.CreateUserDataStreamAsync(ct), cancellationToken);
    }

    public Task<Ticker> Get24hTickerPriceChangeStatisticsAsync(string symbol, CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.Get24hTickerPriceChangeStatisticsAsync(symbol, ct), cancellationToken);
    }

    public Task<IReadOnlyCollection<Ticker>> Get24hTickerPriceChangeStatisticsAsync(CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.Get24hTickerPriceChangeStatisticsAsync(ct), cancellationToken);
    }

    public Task<AccountInfo> GetAccountInfoAsync(CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.GetAccountInfoAsync(ct), cancellationToken);
    }

    public Task<ImmutableSortedSet<AccountTrade>> GetAccountTradesAsync(string symbol, long? fromId, int? limit, CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.GetAccountTradesAsync(symbol, fromId, limit, ct), cancellationToken);
    }

    public Task<IReadOnlyCollection<OrderQueryResult>> GetAllOrdersAsync(string symbol, long? orderId, int? limit, CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.GetAllOrdersAsync(symbol, orderId, limit, ct), cancellationToken);
    }

    public Task<ExchangeInfo> GetExchangeInfoAsync(CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.GetExchangeInfoAsync(ct), cancellationToken);
    }

    public Task<IReadOnlyCollection<SavingsProduct>> GetSavingsProductsAsync(SavingsStatus status, SavingsFeatured featured, long? current, long? size, CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.GetSavingsProductsAsync(status, featured, current, size, ct), cancellationToken);
    }

    public Task<IReadOnlyCollection<SavingsBalance>> GetSavingsBalancesAsync(string asset, CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.GetSavingsBalancesAsync(asset, ct), cancellationToken);
    }

    public Task<IReadOnlyCollection<Kline>> GetKlinesAsync(string symbol, KlineInterval interval, DateTime startTime, DateTime endTime, int limit, CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.GetKlinesAsync(symbol, interval, startTime, endTime, limit, ct), cancellationToken);
    }

    public Task<IReadOnlyCollection<OrderQueryResult>> GetOpenOrdersAsync(string symbol, CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.GetOpenOrdersAsync(symbol, ct), cancellationToken);
    }

    public Task<OrderQueryResult> GetOrderAsync(string symbol, long? orderId, string? originalClientOrderId, CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.GetOrderAsync(symbol, orderId, originalClientOrderId, ct), cancellationToken);
    }

    public Task<SymbolPriceTicker> GetSymbolPriceTickerAsync(string symbol, CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.GetSymbolPriceTickerAsync(symbol, ct), cancellationToken);
    }

    public Task<IReadOnlyCollection<SymbolPriceTicker>> GetSymbolPriceTickersAsync(CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.GetSymbolPriceTickersAsync(ct), cancellationToken);
    }

    public Task PingUserDataStreamAsync(string listenKey, CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.PingUserDataStreamAsync(listenKey, ct), cancellationToken);
    }

    public Task RedeemFlexibleProductAsync(string productId, decimal amount, SavingsRedemptionType type, CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.RedeemFlexibleProductAsync(productId, amount, type, ct), cancellationToken);
    }

    public Task<SavingsQuota?> TryGetLeftDailyRedemptionQuotaOnFlexibleProductAsync(string productId, SavingsRedemptionType type, CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.TryGetLeftDailyRedemptionQuotaOnFlexibleProductAsync(productId, type, ct), cancellationToken);
    }

    public Task<IEnumerable<SwapPool>> GetSwapPoolsAsync(CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.GetSwapPoolsAsync(ct), cancellationToken);
    }

    public Task<SwapPoolLiquidity> GetSwapLiquidityAsync(long poolId, CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.GetSwapLiquidityAsync(poolId, ct), cancellationToken);
    }

    public Task<IEnumerable<SwapPoolLiquidity>> GetSwapLiquiditiesAsync(CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.GetSwapLiquiditiesAsync(ct), cancellationToken);
    }

    public Task<SwapPoolOperation> AddSwapLiquidityAsync(long poolId, SwapPoolLiquidityType type, string asset, decimal quantity, CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.AddSwapLiquidityAsync(poolId, type, asset, quantity, ct), cancellationToken);
    }

    public Task<SwapPoolOperation> RemoveSwapLiquidityAsync(long poolId, SwapPoolLiquidityType type, decimal shareAmount, CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.RemoveSwapLiquidityAsync(poolId, type, shareAmount, ct), cancellationToken);
    }

    public Task<IEnumerable<SwapPoolConfiguration>> GetSwapPoolConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.GetSwapPoolConfigurationsAsync(ct), cancellationToken);
    }

    public Task<SwapPoolLiquidityAddPreview> AddSwapPoolLiquidityPreviewAsync(long poolId, SwapPoolLiquidityType type, string quoteAsset, decimal quoteQuantity, CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.AddSwapPoolLiquidityPreviewAsync(poolId, type, quoteAsset, quoteQuantity, ct), cancellationToken);
    }

    public Task<IReadOnlyCollection<SavingsProduct>> GetSavingsProductsAsync(SavingsStatus status, SavingsFeatured featured, CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.GetSavingsProductsAsync(status, featured, ct), cancellationToken);
    }

    public Task<SwapPoolQuote> GetSwapPoolQuoteAsync(string quoteAsset, string baseAsset, decimal quoteQuantity, CancellationToken cancellationToken = default)
    {
        return WaitAndRetryForeverAsync(ct => _trader.GetSwapPoolQuoteAsync(quoteAsset, baseAsset, quoteQuantity, ct), cancellationToken);
    }

    #region Logging

    [LoggerMessage(0, LogLevel.Warning, "{Type} backing off for {TimeSpan}...")]
    private partial void LogBackingOff(Exception ex, string type, TimeSpan timeSpan);

    #endregion Logging
}