﻿using AutoMapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Trader.Models;
using Trader.Models.Collections;

namespace Trader.Trading.Binance
{
    internal class BinanceTradingService : ITradingService, IHostedService
    {
        private readonly ILogger _logger;
        private readonly BinanceOptions _options;
        private readonly BinanceApiClient _client;
        private readonly IMapper _mapper;

        public BinanceTradingService(ILogger<BinanceTradingService> logger, IOptions<BinanceOptions> options, BinanceApiClient client, IMapper mapper)
        {
            _logger = logger;
            _options = options.Value;
            _client = client;
            _mapper = mapper;
        }

        private static string Name => nameof(BinanceTradingService);

        /// <summary>
        /// Keeps track of the api usage limits.
        /// </summary>
        private readonly ConcurrentDictionary<(RateLimitType Type, TimeSpan Window), UsageTracker> _usage = new();

        public async Task<ExchangeInfo> GetExchangeInfoAsync(CancellationToken cancellationToken = default)
        {
            BinanceApiContext.CaptureUsage = true;

            var output = await _client
                .GetExchangeInfoAsync(cancellationToken)
                .ConfigureAwait(false);

            var result = _mapper.Map<ExchangeInfo>(output);
            var usage = _mapper.Map<ImmutableList<Usage>>(BinanceApiContext.Usage);

            UpdateUsage(usage);

            return result;
        }

        public async Task<SymbolPriceTicker> GetSymbolPriceTickerAsync(string symbol, CancellationToken cancellationToken = default)
        {
            BinanceApiContext.CaptureUsage = true;

            var output = await _client
                .GetSymbolPriceTickerAsync(symbol, cancellationToken)
                .ConfigureAwait(false);

            var result = _mapper.Map<SymbolPriceTicker>(output);

            UpdateUsage(BinanceApiContext.Usage);

            return result;
        }

        public async Task<ImmutableSortedTradeSet> GetAccountTradesAsync(GetAccountTrades model, CancellationToken cancellationToken = default)
        {
            BinanceApiContext.CaptureUsage = true;

            var input = _mapper.Map<AccountTradesRequestModel>(model);

            var output = await _client
                .GetAccountTradesAsync(input, cancellationToken)
                .ConfigureAwait(false);

            var result = _mapper.Map<ImmutableSortedTradeSet>(output);
            var used = _mapper.Map<ImmutableList<Usage>>(BinanceApiContext.Usage);

            UpdateUsage(used);

            return result;
        }

        public async Task<ImmutableSortedOrderSet> GetOpenOrdersAsync(GetOpenOrders model, CancellationToken cancellationToken = default)
        {
            _ = model ?? throw new ArgumentNullException(nameof(model));

            BinanceApiContext.CaptureUsage = true;

            var input = _mapper.Map<GetOpenOrdersRequestModel>(model);

            var output = await _client
                .GetOpenOrdersAsync(input, cancellationToken)
                .ConfigureAwait(false);

            var result = _mapper.Map<ImmutableSortedOrderSet>(output);
            var used = _mapper.Map<ImmutableList<Usage>>(BinanceApiContext.Usage);

            UpdateUsage(used);

            return result;
        }

        public async Task<OrderQueryResult> GetOrderAsync(OrderQuery model, CancellationToken cancellationToken = default)
        {
            _ = model ?? throw new ArgumentNullException(nameof(model));

            BinanceApiContext.CaptureUsage = true;

            var input = _mapper.Map<GetOrderRequestModel>(model);

            var output = await _client
                .GetOrderAsync(input, cancellationToken)
                .ConfigureAwait(false);

            var result = _mapper.Map<OrderQueryResult>(output);
            var used = _mapper.Map<ImmutableList<Usage>>(BinanceApiContext.Usage);

            UpdateUsage(used);

            return result;
        }

        public async Task<ImmutableSortedOrderSet> GetAllOrdersAsync(GetAllOrders model, CancellationToken cancellationToken = default)
        {
            _ = model ?? throw new ArgumentNullException(nameof(model));

            BinanceApiContext.CaptureUsage = true;

            var input = _mapper.Map<GetAllOrdersRequestModel>(model);

            var output = await _client
                .GetAllOrdersAsync(input, cancellationToken)
                .ConfigureAwait(false);

            var result = _mapper.Map<ImmutableSortedOrderSet>(output);
            var used = _mapper.Map<ImmutableList<Usage>>(BinanceApiContext.Usage);

            UpdateUsage(used);

            return result;
        }

        public async Task<OrderResult> CreateOrderAsync(Order model, CancellationToken cancellationToken = default)
        {
            _ = model ?? throw new ArgumentNullException(nameof(model));

            BinanceApiContext.CaptureUsage = true;

            var input = _mapper.Map<NewOrderRequestModel>(model);

            var output = await _client
                .CreateOrderAsync(input, cancellationToken)
                .ConfigureAwait(false);

            var result = _mapper.Map<OrderResult>(output);

            UpdateUsage(BinanceApiContext.Usage);

            return result;
        }

        public async Task<CancelStandardOrderResult> CancelOrderAsync(CancelStandardOrder model, CancellationToken cancellationToken = default)
        {
            _ = model ?? throw new ArgumentNullException(nameof(model));

            BinanceApiContext.CaptureUsage = true;

            var input = _mapper.Map<CancelOrderRequestModel>(model);

            var output = await _client
                .CancelOrderAsync(input, cancellationToken)
                .ConfigureAwait(false);

            var result = _mapper.Map<CancelStandardOrderResult>(output);

            UpdateUsage(BinanceApiContext.Usage);

            return result;
        }

        public async Task<AccountInfo> GetAccountInfoAsync(GetAccountInfo model, CancellationToken cancellationToken = default)
        {
            _ = model ?? throw new ArgumentNullException(nameof(model));

            BinanceApiContext.CaptureUsage = true;

            var input = _mapper.Map<AccountRequestModel>(model);

            var output = await _client
                .GetAccountInfoAsync(input, cancellationToken)
                .ConfigureAwait(false);

            var result = _mapper.Map<AccountInfo>(output);
            var used = _mapper.Map<ImmutableList<Usage>>(BinanceApiContext.Usage);

            UpdateUsage(used);

            return result;
        }

        public async Task<string> CreateUserDataStreamAsync(CancellationToken cancellationToken = default)
        {
            BinanceApiContext.CaptureUsage = true;

            var output = await _client
                .CreateUserDataStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            UpdateUsage(_mapper.Map<ImmutableList<Usage>>(BinanceApiContext.Usage));

            return output.ListenKey;
        }

        public async Task PingUserDataStreamAsync(string listenKey, CancellationToken cancellationToken = default)
        {
            _ = listenKey ?? throw new ArgumentNullException(nameof(listenKey));

            BinanceApiContext.CaptureUsage = true;
            BinanceApiContext.SkipSigning = true;

            await _client
                .PingUserDataStreamAsync(new ListenKeyRequestModel(listenKey), cancellationToken)
                .ConfigureAwait(false);

            UpdateUsage(_mapper.Map<ImmutableList<Usage>>(BinanceApiContext.Usage));
        }

        public async Task CloseUserDataStreamAsync(string listenKey, CancellationToken cancellationToken = default)
        {
            _ = listenKey ?? throw new ArgumentNullException(nameof(listenKey));

            BinanceApiContext.CaptureUsage = true;
            BinanceApiContext.SkipSigning = true;

            await _client
                .CloseUserDataStreamAsync(new ListenKeyRequestModel(listenKey), cancellationToken)
                .ConfigureAwait(false);

            UpdateUsage(_mapper.Map<ImmutableList<Usage>>(BinanceApiContext.Usage));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return SyncLimitsAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        #region Helpers

        private async Task SyncLimitsAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("{Name} querying exchange rate limits...", Name);

            // get the exchange request limits
            var result = await _client
                .GetExchangeInfoAsync(cancellationToken)
                .ConfigureAwait(false);

            var info = _mapper.Map<ExchangeInfo>(result);

            // keep the request weight limits
            foreach (var limit in info.RateLimits)
            {
                if (limit.Limit == 0)
                {
                    throw new BinanceException("Received unexpected rate limit of zero from the exchange");
                }

                _usage[(limit.Type, limit.TimeSpan)] = new UsageTracker
                {
                    Limit = limit.Limit
                };
            }
        }

        private void UpdateUsage(IEnumerable<Usage>? used)
        {
            if (used is null) return;

            // update with the usage of the last request
            foreach (var item in used)
            {
                if (!_usage.TryGetValue((item.Type, item.Window), out var tracker))
                {
                    _logger.LogWarning(
                        "{Name} received a rate limit usage value for an unknown limit of ({RateLimitType}, {Window})",
                        Name, item.Type, item.Window);

                    continue;
                }

                tracker.Used = item.Count;
            }

            // analyse the usages
            foreach (var item in _usage)
            {
                var ratio = item.Value.Used / (double)item.Value.Limit;

                if (ratio > _options.UsageWarningRatio)
                {
                    _logger.LogWarning(
                        "{Name} detected rate limit usage for {RateLimitType} {Window} is at {Usage:P2}",
                        Name, item.Key.Type, item.Key.Window, ratio);
                }
            }

            // log the usages
            _logger.LogDebug("{Name} reports limit usage as {@Usages}", Name, _usage);
        }

        #endregion Helpers

        #region Classes

        private class UsageTracker
        {
            public int Limit { get; set; }
            public int Used { get; set; }
        }

        #endregion Classes
    }
}