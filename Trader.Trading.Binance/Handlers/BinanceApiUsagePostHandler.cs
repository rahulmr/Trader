﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Outcompute.Trader.Core.Time;
using Outcompute.Trader.Models;
using System;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Outcompute.Trader.Trading.Binance.Handlers
{
    internal partial class BinanceApiUsagePostHandler : DelegatingHandler
    {
        private readonly BinanceOptions _options;
        private readonly BinanceUsageContext _usage;
        private readonly ILogger _logger;
        private readonly ISystemClock _clock;

        public BinanceApiUsagePostHandler(IOptions<BinanceOptions> options, BinanceUsageContext usage, ILogger<BinanceApiUsagePostHandler> logger, ISystemClock clock)
        {
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _usage = usage ?? throw new ArgumentNullException(nameof(usage));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        private const string TypeName = nameof(BinanceApiUsagePostHandler);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            foreach (var header in response.Headers)
            {
                if (header.Key.StartsWith(_options.UsedRequestWeightHeaderPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var key = header.Key[_options.UsedRequestWeightHeaderPrefix.Length..];
                    var unit = key[^1..].ToUpperInvariant();
                    var value = int.Parse(key[..^1], CultureInfo.InvariantCulture);

                    var window = unit switch
                    {
                        "S" => TimeSpan.FromSeconds(value),
                        "M" => TimeSpan.FromMinutes(value),
                        "H" => TimeSpan.FromHours(value),
                        "D" => TimeSpan.FromDays(value),
                        _ => throw new InvalidOperationException()
                    };

                    foreach (var item in header.Value)
                    {
                        var weight = int.Parse(item, CultureInfo.InvariantCulture);

                        _usage.SetUsed(RateLimitType.RequestWeight, window, weight, _clock.UtcNow);
                    }
                }
                else if (header.Key.StartsWith(_options.UsedOrderCountHeaderPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var key = header.Key[_options.UsedOrderCountHeaderPrefix.Length..];
                    var unit = key[^1..].ToUpperInvariant();
                    var value = int.Parse(key[..^1], CultureInfo.InvariantCulture);

                    var window = unit switch
                    {
                        "S" => TimeSpan.FromSeconds(value),
                        "M" => TimeSpan.FromMinutes(value),
                        "H" => TimeSpan.FromHours(value),
                        "D" => TimeSpan.FromDays(value),
                        _ => throw new InvalidOperationException()
                    };

                    foreach (var item in header.Value)
                    {
                        var count = int.Parse(item, CultureInfo.InvariantCulture);

                        _usage.SetUsed(RateLimitType.Orders, window, count, _clock.UtcNow);
                    }
                }
            }

            // analyse the usages
            foreach (var item in _usage.EnumerateAll())
            {
                // skip expired usages
                if (item.Updated.Add(item.Window) < _clock.UtcNow)
                {
                    continue;
                }

                var ratio = item.Used / (double)item.Limit;

                if (ratio >= _options.UsageWarningRatio)
                {
                    LogDetectedRateLimitUsage(TypeName, item.Type, item.Window, ratio);

                    // force backoff once the safety limit is reached
                    if (ratio >= _options.UsageBackoffRatio)
                    {
                        LogDetectedRateLimitUsageOverLimit(TypeName, item.Type, item.Window, _options.UsageBackoffRatio);

                        TimeSpan retry;
                        var now = _clock.UtcNow;

                        if (item.Window == TimeSpan.FromMinutes(1))
                        {
                            retry = now.AddMinutes(1).AddSeconds(-now.Second).Subtract(now);
                        }
                        else if (item.Window == TimeSpan.FromHours(1))
                        {
                            retry = now.AddHours(1).AddMinutes(-now.Minute).AddSeconds(-now.Second).Subtract(now);
                        }
                        else if (item.Window == TimeSpan.FromDays(1))
                        {
                            retry = now.AddDays(1).AddHours(-now.Hour).AddMinutes(-now.Minute).AddSeconds(-now.Second).Subtract(now);
                        }
                        else
                        {
                            retry = _options.DefaultBackoffPeriod;

                            LogDetectedViolationOfUnknownRateWindow(TypeName, item.Type, item.Window, retry);
                        }

                        throw new BinanceTooManyRequestsException(retry);
                    }
                }
            }

            return response;
        }

        [LoggerMessage(0, LogLevel.Warning, "{TypeName} detected rate limit usage for {RateLimitType} {Window} is at {Usage:P2}")]
        private partial void LogDetectedRateLimitUsage(string typeName, RateLimitType rateLimitType, TimeSpan window, double usage);

        [LoggerMessage(1, LogLevel.Warning, "{TypeName} detected rate limit usage for {RateLimitType} {Window} is over the limit of {Limit:P2} and will force backoff")]
        private partial void LogDetectedRateLimitUsageOverLimit(string typeName, RateLimitType rateLimitType, TimeSpan window, double limit);

        [LoggerMessage(2, LogLevel.Error, "{TypeName} detected violation of unknown rate window {RateLimitType} {Window} and will force the default backoff of {Backoff}")]
        private partial void LogDetectedViolationOfUnknownRateWindow(string typeName, RateLimitType rateLimitType, TimeSpan window, TimeSpan backoff);
    }
}