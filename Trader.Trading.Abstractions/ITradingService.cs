﻿namespace Outcompute.Trader.Trading;

public interface ITradingService
{
    #region Savings

    Task<IReadOnlyCollection<SavingsProduct>> GetSavingsProductsAsync(
        SavingsStatus status,
        SavingsFeatured featured,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<SavingsProduct>> GetSavingsProductsAsync(
        SavingsStatus status,
        SavingsFeatured featured,
        long? current,
        long? size,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<SavingsBalance>> GetSavingsBalancesAsync(
        string asset,
        CancellationToken cancellationToken = default);

    Task<SavingsQuota?> TryGetLeftDailyRedemptionQuotaOnFlexibleProductAsync(
        string productId,
        SavingsRedemptionType type,
        CancellationToken cancellationToken = default);

    #endregion Savings

    /// <summary>
    /// Enables automatic backoff upon "too many requests" exceptions for the next request as implemented by the provider.
    /// </summary>
    ITradingService WithBackoff();

    Task<ExchangeInfo> GetExchangeInfoAsync(
        CancellationToken cancellationToken = default);

    Task<SymbolPriceTicker> GetSymbolPriceTickerAsync(
        string symbol,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<SymbolPriceTicker>> GetSymbolPriceTickersAsync(
        CancellationToken cancellationToken = default);

    Task<ImmutableSortedSet<AccountTrade>> GetAccountTradesAsync(
        string symbol,
        long? fromId,
        int? limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<OrderQueryResult>> GetOpenOrdersAsync(
        string symbol,
        CancellationToken cancellationToken = default);

    Task<OrderQueryResult> GetOrderAsync(
        string symbol,
        long? orderId,
        string? originalClientOrderId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<OrderQueryResult>> GetAllOrdersAsync(
        string symbol,
        long? orderId,
        int? limit,
        CancellationToken cancellationToken = default);

    Task<CancelStandardOrderResult> CancelOrderAsync(
        string symbol,
        long orderId,
        CancellationToken cancellationToken = default);

    Task<OrderResult> CreateOrderAsync(
        string symbol,
        OrderSide side,
        OrderType type,
        TimeInForce? timeInForce,
        decimal? quantity,
        decimal? quoteOrderQuantity,
        decimal? price,
        string? newClientOrderId,
        decimal? stopPrice,
        decimal? icebergQuantity,
        CancellationToken cancellationToken = default);

    Task<AccountInfo> GetAccountInfoAsync(
        CancellationToken cancellationToken = default);

    Task<Ticker> Get24hTickerPriceChangeStatisticsAsync(
        string symbol,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Ticker>> Get24hTickerPriceChangeStatisticsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Kline>> GetKlinesAsync(
        string symbol,
        KlineInterval interval,
        DateTime startTime,
        DateTime endTime,
        int limit,
        CancellationToken cancellationToken = default);

    Task RedeemFlexibleProductAsync(
        string productId,
        decimal amount,
        SavingsRedemptionType type,
        CancellationToken cancellationToken = default);

    Task<string> CreateUserDataStreamAsync(
        CancellationToken cancellationToken = default);

    Task PingUserDataStreamAsync(
        string listenKey,
        CancellationToken cancellationToken = default);

    Task CloseUserDataStreamAsync(
        string listenKey,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<SwapPool>> GetSwapPoolsAsync(
        CancellationToken cancellationToken = default);

    Task<SwapPoolLiquidity> GetSwapLiquidityAsync(
        long poolId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<SwapPoolLiquidity>> GetSwapLiquiditiesAsync(
        CancellationToken cancellationToken = default);

    Task<SwapPoolOperation> AddSwapLiquidityAsync(
        long poolId,
        SwapPoolLiquidityType type,
        string asset,
        decimal quantity,
        CancellationToken cancellationToken = default);

    Task<SwapPoolOperation> RemoveSwapLiquidityAsync(
        long poolId,
        SwapPoolLiquidityType type,
        decimal shareAmount,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<SwapPoolConfiguration>> GetSwapPoolConfigurationsAsync(
        CancellationToken cancellationToken = default);

    Task<SwapPoolLiquidityAddPreview> AddSwapPoolLiquidityPreviewAsync(
        long poolId,
        SwapPoolLiquidityType type,
        string quoteAsset,
        decimal quoteQuantity,
        CancellationToken cancellationToken = default);

    Task<SwapPoolQuote> GetSwapPoolQuoteAsync(
        string quoteAsset,
        string baseAsset,
        decimal quoteQuantity,
        CancellationToken cancellationToken = default);
}