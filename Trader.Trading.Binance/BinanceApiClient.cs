﻿using AutoMapper;
using FastMember;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Outcompute.Trader.Models;
using System.Collections.Immutable;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Web;

namespace Outcompute.Trader.Trading.Binance;

internal class BinanceApiClient
{
    private readonly BinanceOptions _options;
    private readonly HttpClient _client;
    private readonly IMapper _mapper;
    private readonly ObjectPool<StringBuilder> _pool;

    public BinanceApiClient(IOptions<BinanceOptions> options, HttpClient client, IMapper mapper, ObjectPool<StringBuilder> pool)
    {
        _options = options.Value;
        _client = client;
        _mapper = mapper;
        _pool = pool;
    }

    #region General Endpoints

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        var response = await _client
            .GetAsync(
                new Uri("/api/v3/ping", UriKind.Relative),
                cancellationToken)
            .ConfigureAwait(false);

        return response.IsSuccessStatusCode;
    }

    public async Task<DateTime> GetTimeAsync(CancellationToken cancellationToken = default)
    {
        var result = await _client
            .GetFromJsonAsync<ApiServerTime>(
                new Uri("/api/v3/time", UriKind.Relative),
                cancellationToken)
            .ConfigureAwait(false);

        return _mapper.Map<DateTime>(result);
    }

    public async Task<ApiExchangeInfo> GetExchangeInfoAsync(CancellationToken cancellationToken = default)
    {
        return await _client
            .GetFromJsonAsync<ApiExchangeInfo>(
                new Uri("/api/v3/exchangeInfo", UriKind.Relative),
                cancellationToken)
            .ConfigureAwait(false) ?? throw new BinanceUnknownResponseException();
    }

    #endregion General Endpoints

    #region Market Data Endpoints

    public async Task<OrderBook> GetOrderBookAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var result = await _client
            .GetFromJsonAsync<ApiOrderBook>(
                new Uri($"/api/v3/depth?symbol={HttpUtility.UrlEncode(symbol)}", UriKind.Relative),
                cancellationToken)
            .ConfigureAwait(false);

        return _mapper.Map<OrderBook>(result);
    }

    public async Task<IEnumerable<Trade>> GetHistoricalTradesAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var result = await _client
            .GetFromJsonAsync<ApiTrade[]>(
                new Uri($"/api/v3/historicalTrades?symbol={HttpUtility.UrlEncode(symbol)}", UriKind.Relative),
                cancellationToken)
            .ConfigureAwait(false);

        return _mapper.Map<IEnumerable<Trade>>(result);
    }

    public async Task<ApiTicker> Get24hTickerPriceChangeStatisticsAsync(string symbol, CancellationToken cancellationToken = default)
    {
        _ = symbol ?? throw new ArgumentNullException(nameof(symbol));

        return await _client
            .GetFromJsonAsync<ApiTicker>(
                new Uri($"/api/v3/ticker/24hr?symbol={HttpUtility.UrlEncode(symbol)}", UriKind.Relative),
                cancellationToken)
            .ConfigureAwait(false) ?? throw new BinanceUnknownResponseException();
    }

    public async Task<IReadOnlyCollection<ApiTicker>> Get24hTickerPriceChangeStatisticsAsync(CancellationToken cancellationToken = default)
    {
        return await _client
            .GetFromJsonAsync<ApiTicker[]>(
                new Uri($"/api/v3/ticker/24hr", UriKind.Relative),
                cancellationToken)
            .ConfigureAwait(false) ?? throw new BinanceUnknownResponseException();
    }

    public async Task<ApiSymbolPriceTicker> GetSymbolPriceTickerAsync(string symbol, CancellationToken cancellationToken = default)
    {
        _ = symbol ?? throw new ArgumentNullException(nameof(symbol));

        BinanceApiContext.SkipSigning = true;

        return await _client
            .GetFromJsonAsync<ApiSymbolPriceTicker>(
                new Uri($"/api/v3/ticker/price?symbol={HttpUtility.UrlEncode(symbol)}", UriKind.Relative),
                cancellationToken)
            .ConfigureAwait(false) ?? throw new BinanceUnknownResponseException();
    }

    public async Task<IEnumerable<SymbolPriceTicker>> GetSymbolPriceTickersAsync(CancellationToken cancellationToken = default)
    {
        var result = await _client
            .GetFromJsonAsync<IEnumerable<ApiSymbolPriceTicker>>(
                new Uri($"/api/v3/ticker/price", UriKind.Relative),
                cancellationToken)
            .ConfigureAwait(false);

        return _mapper.Map<IEnumerable<SymbolPriceTicker>>(result);
    }

    public async Task<SymbolOrderBookTicker> GetSymbolOrderBookTickerAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var result = await _client
            .GetFromJsonAsync<ApiSymbolOrderBookTicker>(
                new Uri($"/api/v3/ticker/bookTicker?symbol={HttpUtility.UrlEncode(symbol)}", UriKind.Relative),
                cancellationToken)
            .ConfigureAwait(false);

        return _mapper.Map<SymbolOrderBookTicker>(result);
    }

    public async Task<IEnumerable<SymbolOrderBookTicker>> GetSymbolOrderBookTickersAsync(CancellationToken cancellationToken = default)
    {
        var result = await _client
            .GetFromJsonAsync<IEnumerable<ApiSymbolOrderBookTicker>>(
                new Uri($"/api/v3/ticker/bookTicker", UriKind.Relative),
                cancellationToken)
            .ConfigureAwait(false);

        return _mapper.Map<IEnumerable<SymbolOrderBookTicker>>(result);
    }

    #endregion Market Data Endpoints

    #region Account Endpoints

    /// <summary>
    /// Creates the specified order.
    /// </summary>
    public async Task<CreateOrderResponse> CreateOrderAsync(CreateOrderRequest model, CancellationToken cancellationToken = default)
    {
        _ = model ?? throw new ArgumentNullException(nameof(model));
        _ = model.Symbol ?? throw new ArgumentException($"{nameof(OrderQuery.Symbol)} is required");

        var response = await _client
            .PostAsync(
                Combine(new Uri("/api/v3/order", UriKind.Relative), model),
                EmptyHttpContent.Instance,
                cancellationToken)
            .ConfigureAwait(false);

        return await response.Content
            .ReadFromJsonAsync<CreateOrderResponse>(_options.JsonSerializerOptions, cancellationToken)
            .ConfigureAwait(false) ?? throw new BinanceUnknownResponseException();
    }

    /// <summary>
    /// Gets the status of the specified order.
    /// </summary>
    public async Task<GetOrderResponse> GetOrderAsync(GetOrderRequest model, CancellationToken cancellationToken = default)
    {
        _ = model ?? throw new ArgumentNullException(nameof(model));
        _ = model.Symbol ?? throw new ArgumentException($"{nameof(OrderQuery.Symbol)} is required");

        return await _client
            .GetFromJsonAsync<GetOrderResponse>(
                Combine(new Uri("/api/v3/order", UriKind.Relative), model),
                _options.JsonSerializerOptions,
                cancellationToken)
            .ConfigureAwait(false) ?? throw new BinanceUnknownResponseException();
    }

    /// <summary>
    /// Cancels the specified order.
    /// </summary>
    public async Task<CancelOrderResponse> CancelOrderAsync(CancelOrderRequest model, CancellationToken cancellationToken = default)
    {
        _ = model ?? throw new ArgumentNullException(nameof(model));
        _ = model.Symbol ?? throw new ArgumentException($"{nameof(OrderQuery.Symbol)} is required");

        var output = await _client
            .DeleteAsync(
                Combine(new Uri("/api/v3/order", UriKind.Relative), model),
                cancellationToken)
            .ConfigureAwait(false);

        return await output.Content
            .ReadFromJsonAsync<CancelOrderResponse>(_options.JsonSerializerOptions, cancellationToken)
            .ConfigureAwait(false) ?? throw new BinanceUnknownResponseException();
    }

    /// <summary>
    /// Cancels all open orders.
    /// </summary>
    public async Task<IEnumerable<CancelAllOrdersResponse>> CancelAllOrdersAsync(CancelAllOrdersRequest model, CancellationToken cancellationToken = default)
    {
        var uri = Combine(new Uri("/api/v3/openOrders", UriKind.Relative), model);

        var response = await _client
            .DeleteAsync(uri, cancellationToken)
            .ConfigureAwait(false);

        return await response.Content
            .ReadFromJsonAsync<IEnumerable<CancelAllOrdersResponse>>(_options.JsonSerializerOptions, cancellationToken)
            .WithNullHandling()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all open orders.
    /// </summary>
    public async Task<IEnumerable<GetOrderResponse>> GetOpenOrdersAsync(GetOpenOrdersRequest model, CancellationToken cancellationToken = default)
    {
        _ = model ?? throw new ArgumentNullException(nameof(model));
        _ = model.Symbol ?? throw new ArgumentException($"{nameof(GetOpenOrders.Symbol)} is required");

        return await _client
            .GetFromJsonAsync<IEnumerable<GetOrderResponse>>(
                Combine(new Uri("/api/v3/openOrders", UriKind.Relative), model),
                _options.JsonSerializerOptions,
                cancellationToken)
            .ConfigureAwait(false) ?? throw new BinanceUnknownResponseException();
    }

    /// <summary>
    /// Gets all orders.
    /// </summary>
    public async Task<IEnumerable<GetOrderResponse>> GetAllOrdersAsync(GetAllOrdersRequest model, CancellationToken cancellationToken = default)
    {
        _ = model ?? throw new ArgumentNullException(nameof(model));
        _ = model.Symbol ?? throw new ArgumentException($"{nameof(GetOpenOrders.Symbol)} is required");

        return await _client
            .GetFromJsonAsync<IEnumerable<GetOrderResponse>>(
                Combine(new Uri("/api/v3/allOrders", UriKind.Relative), model),
                _options.JsonSerializerOptions,
                cancellationToken)
            .ConfigureAwait(false) ?? throw new BinanceUnknownResponseException();
    }

    /// <summary>
    /// Gets the account information.
    /// </summary>
    public async Task<GetAccountInfoResponse> GetAccountInfoAsync(GetAccountInfoRequest model, CancellationToken cancellationToken = default)
    {
        _ = model ?? throw new ArgumentNullException(nameof(model));

        return await _client
            .GetFromJsonAsync<GetAccountInfoResponse>(
                Combine(new Uri("/api/v3/account", UriKind.Relative), model),
                _options.JsonSerializerOptions,
                cancellationToken)
            .ConfigureAwait(false) ?? throw new BinanceUnknownResponseException();
    }

    /// <summary>
    /// Gets the account trades.
    /// </summary>
    public async Task<IEnumerable<GetAccountTradesResponse>> GetAccountTradesAsync(GetAccountTradesRequest model, CancellationToken cancellationToken = default)
    {
        _ = model ?? throw new ArgumentNullException(nameof(model));
        _ = model.Symbol ?? throw new ArgumentException($"{nameof(GetAccountTradesRequest.Symbol)} is required");

        return await _client
            .GetFromJsonAsync<IEnumerable<GetAccountTradesResponse>>(
                Combine(new Uri("/api/v3/myTrades", UriKind.Relative), model),
                cancellationToken)
            .ConfigureAwait(false) ?? throw new BinanceUnknownResponseException();
    }

    public async Task<IEnumerable<GetKlinesResponse>> GetKlinesAsync(GetKlinesRequest model, CancellationToken cancellationToken = default)
    {
        _ = model ?? throw new ArgumentNullException(nameof(model));

        var response = await _client
            .GetFromJsonAsync<IEnumerable<JsonElement[]>>(Combine(new Uri("/api/v3/klines", UriKind.Relative), model), cancellationToken)
            .ConfigureAwait(false);

        return _mapper.Map<IEnumerable<GetKlinesResponse>>(response);
    }

    /// <summary>
    /// Starts a new user data stream and returns the listen key for it.
    /// </summary>
    public async Task<CreateUserDataStreamResponse> CreateUserDataStreamAsync(CancellationToken cancellationToken = default)
    {
        var response = await _client
            .PostAsync(
                new Uri("/api/v3/userDataStream", UriKind.Relative),
                EmptyHttpContent.Instance,
                cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<CreateUserDataStreamResponse>(_options.JsonSerializerOptions, cancellationToken)
            .ConfigureAwait(false) ?? throw new BinanceUnknownResponseException();
    }

    public async Task PingUserDataStreamAsync(PingUserDataStreamRequest model, CancellationToken cancellationToken = default)
    {
        _ = model ?? throw new ArgumentNullException(nameof(model));

        await _client
            .PutAsync(
                Combine(new Uri("/api/v3/userDataStream", UriKind.Relative), model),
                EmptyHttpContent.Instance,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task CloseUserDataStreamAsync(CloseUserDataStreamRequest model, CancellationToken cancellationToken = default)
    {
        _ = model ?? throw new ArgumentNullException(nameof(model));

        await _client
            .DeleteAsync(
                Combine(new Uri("/api/v3/userDataStream", UriKind.Relative), model),
                cancellationToken)
            .ConfigureAwait(false);
    }

    #endregion Account Endpoints

    #region Savings Endpoints

    public async Task<GetLeftDailyRedemptionQuotaOnFlexibleProductResponse?> GetLeftDailyRedemptionQuotaOnFlexibleProductAsync(GetLeftDailyRedemptionQuotaOnFlexibleProductRequest model, CancellationToken cancellationToken = default)
    {
        _ = model ?? throw new ArgumentNullException(nameof(model));

        try
        {
            return await _client
                .GetFromJsonAsync<GetLeftDailyRedemptionQuotaOnFlexibleProductResponse>(Combine(new Uri("/sapi/v1/lending/daily/userRedemptionQuota", UriKind.Relative), model), cancellationToken)
                .ConfigureAwait(false) ?? throw new BinanceUnknownResponseException();
        }
        catch (BinanceCodeException ex) when (ex.BinanceCode == -6001)
        {
            // handle "daily product does not exist" response
            return null;
        }
    }

    public async Task<IEnumerable<GetFlexibleProductPositionsResponse>> GetFlexibleProductPositionsAsync(GetFlexibleProductPositionsRequest model, CancellationToken cancellationToken = default)
    {
        _ = model ?? throw new ArgumentNullException(nameof(model));

        return await _client
            .GetFromJsonAsync<IEnumerable<GetFlexibleProductPositionsResponse>>(Combine(new Uri("/sapi/v1/lending/daily/token/position", UriKind.Relative), model), cancellationToken)
            .ConfigureAwait(false) ?? throw new BinanceUnknownResponseException();
    }

    public async Task RedeemFlexibleProductAsync(RedeemFlexibleProductRequest model, CancellationToken cancellationToken = default)
    {
        _ = model ?? throw new ArgumentNullException(nameof(model));

        var response = await _client
            .PostAsync(Combine(new Uri("/sapi/v1/lending/daily/redeem", UriKind.Relative), model), EmptyHttpContent.Instance, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    private readonly Uri _getFlexibleProductListUri = new("/sapi/v1/lending/daily/product/list", UriKind.Relative);

    public async Task<IEnumerable<GetFlexibleProductListResponse>> GetFlexibleProductListAsync(GetFlexibleProductListRequest model, CancellationToken cancellationToken = default)
    {
        _ = model ?? throw new ArgumentNullException(nameof(model));

        var uri = Combine(_getFlexibleProductListUri, model);

        return await _client
            .GetFromJsonAsync<IEnumerable<GetFlexibleProductListResponse>>(uri, cancellationToken)
            .ConfigureAwait(false) ?? throw new BinanceUnknownResponseException();
    }

    #endregion Savings Endpoints

    #region Swap Endpoints

    public async Task<IEnumerable<GetSwapPoolsResponse>> GetSwapPoolsAsync(CancellationToken cancellationToken = default)
    {
        var uri = new Uri("/sapi/v1/bswap/pools", UriKind.Relative);

        return await _client
            .GetFromJsonAsync<IEnumerable<GetSwapPoolsResponse>>(uri, cancellationToken)
            .WithNullHandling();
    }

    public Task<GetSwapPoolLiquidityResponse> GetSwapPoolLiquidityAsync(GetSwapPoolLiquidityRequest model, CancellationToken cancellationToken = default)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));

        var uri = Combine(new Uri("/sapi/v1/bswap/liquidity", UriKind.Relative), model);

        return _client
            .GetFromJsonAsync<GetSwapPoolLiquidityResponse>(uri, cancellationToken)
            .WithNullHandling();
    }

    public Task<IEnumerable<GetSwapPoolLiquidityResponse>> GetSwapPoolsLiquiditiesAsync(GetSwapPoolLiquidityRequest model, CancellationToken cancellationToken = default)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));

        var uri = Combine(new Uri("/sapi/v1/bswap/liquidity", UriKind.Relative), model);

        return _client
            .GetFromJsonAsync<IEnumerable<GetSwapPoolLiquidityResponse>>(uri, cancellationToken)
            .WithNullHandling();
    }

    public async Task<AddSwapPoolLiquidityResponse> AddSwapPoolLiquidityAsync(AddSwapPoolLiquidityRequest model, CancellationToken cancellationToken = default)
    {
        var uri = Combine(new Uri("/sapi/v1/bswap/liquidityAdd", UriKind.Relative), model);

        var result = await _client
            .PostAsync(uri, EmptyHttpContent.Instance, cancellationToken)
            .ConfigureAwait(false);

        result = result.EnsureSuccessStatusCode();

        return await result.Content
            .ReadFromJsonAsync<AddSwapPoolLiquidityResponse>(_options.JsonSerializerOptions, cancellationToken)
            .WithNullHandling()
            .ConfigureAwait(false);
    }

    public async Task<RemoveSwapPoolLiquidityResponse> RemoveSwapPoolLiquidityAsync(RemoveSwapPoolLiquidityRequest model, CancellationToken cancellationToken = default)
    {
        var uri = Combine(new Uri("/sapi/v1/bswap/liquidityRemove", UriKind.Relative), model);

        var result = await _client
            .PostAsync(uri, EmptyHttpContent.Instance, cancellationToken)
            .ConfigureAwait(false);

        result = result.EnsureSuccessStatusCode();

        return await result.Content
            .ReadFromJsonAsync<RemoveSwapPoolLiquidityResponse>(_options.JsonSerializerOptions, cancellationToken)
            .WithNullHandling()
            .ConfigureAwait(false);
    }

    public async Task<IEnumerable<GetSwapPoolConfigurationResponse>> GetSwapPoolConfigurationsAsync(GetSwapPoolConfigurationRequest model, CancellationToken cancellationToken = default)
    {
        var uri = Combine(new Uri("/sapi/v1/bswap/poolConfigure", UriKind.Relative), model);

        return await _client
            .GetFromJsonAsync<IEnumerable<GetSwapPoolConfigurationResponse>>(uri, cancellationToken)
            .WithNullHandling()
            .ConfigureAwait(false);
    }

    public async Task<AddSwapPoolLiquidityPreviewResponse> AddSwapPoolLiquidityPreviewAsync(AddSwapPoolLiquidityPreviewRequest model, CancellationToken cancellationToken = default)
    {
        var uri = Combine(new Uri("/sapi/v1/bswap/addLiquidityPreview", UriKind.Relative), model);

        return await _client
            .GetFromJsonAsync<AddSwapPoolLiquidityPreviewResponse>(uri, cancellationToken)
            .WithNullHandling()
            .ConfigureAwait(false);
    }

    public async Task<GetSwapPoolQuoteResponse> GetSwapPoolQuoteAsync(GetSwapPoolQuoteRequest model, CancellationToken cancellationToken = default)
    {
        var uri = Combine(new Uri("/sapi/v1/bswap/quote", UriKind.Relative), model);

        return await _client
            .GetFromJsonAsync<GetSwapPoolQuoteResponse>(uri, cancellationToken)
            .WithNullHandling()
            .ConfigureAwait(false);
    }

    #endregion Swap Endpoints

    #region Helpers

    /// <summary>
    /// Caches type data at zero lookup cost.
    /// </summary>
    [SuppressMessage("Minor Code Smell", "S3260:Non-derived \"private\" classes and records should be \"sealed\"", Justification = "Type Cache Pattern")]
    private static class TypeCache<T>
    {
        public static TypeAccessor TypeAccessor { get; } = TypeAccessor.Create(typeof(T));

        public static ImmutableArray<(string Name, string LowerName)> Names { get; } = TypeCache<T>.TypeAccessor
            .GetMembers()
            .Select(x => (x.Name, char.ToLowerInvariant(x.Name[0]) + x.Name[1..]))
            .ToImmutableArray();
    }

    private Uri Combine<T>(Uri requestUri, T data)
    {
        var builder = _pool.Get();

        builder.Append('?');

        try
        {
            var next = false;

            foreach (var (name, lowerName) in TypeCache<T>.Names)
            {
                var value = TypeCache<T>.TypeAccessor[data, name];

                if (value is not null)
                {
                    if (next)
                    {
                        builder.Append('&');
                    }

                    builder.Append(lowerName).Append('=').Append(value);

                    next = true;
                }
            }

            return new Uri(requestUri.ToString() + builder.ToString(), requestUri.IsAbsoluteUri ? UriKind.Absolute : UriKind.Relative);
        }
        finally
        {
            _pool.Return(builder);
        }
    }

    private async Task<TResult> DeleteAsync<TRequest, TResponse, TResult>(Uri requestUri, object data, CancellationToken cancellationToken = default)
    {
        var request = _mapper.Map<TRequest>(data);

        var response = await _client
            .DeleteAsync(Combine(requestUri, request), cancellationToken)
            .ConfigureAwait(false);

        var typed = await response.Content
            .ReadFromJsonAsync<TResponse>(_options.JsonSerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        return _mapper.Map<TResult>(typed);
    }

    #endregion Helpers
}

internal static class BinanceApiClientExtensions
{
    public static async Task<T> WithNullHandling<T>(this Task<T?> task)
    {
        var result = await task.ConfigureAwait(false);
        return result ?? throw new BinanceUnknownResponseException();
    }
}