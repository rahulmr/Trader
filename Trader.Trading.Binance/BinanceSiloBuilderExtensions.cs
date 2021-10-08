﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Outcompute.Trader.Trading;
using Outcompute.Trader.Trading.Binance;
using Outcompute.Trader.Trading.Binance.Converters;
using Outcompute.Trader.Trading.Binance.Handlers;
using Outcompute.Trader.Trading.Binance.Providers.MarketData;
using Outcompute.Trader.Trading.Binance.Providers.Savings;
using Outcompute.Trader.Trading.Binance.Providers.UserData;
using Outcompute.Trader.Trading.Binance.Signing;
using Outcompute.Trader.Trading.Providers;
using System;

namespace Orleans.Hosting
{
    public static class BinanceSiloBuilderExtensions
    {
        public static ISiloBuilder AddBinanceTradingService(this ISiloBuilder builder, Action<BinanceOptions> configure)
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            // add the kitchen sink
            builder

                .ConfigureApplicationParts(manager => manager.AddApplicationPart(typeof(BinanceSiloBuilderExtensions).Assembly).WithReferences())
                .ConfigureServices(services =>
                {
                    services

                        // add options
                        .AddOptions<BinanceOptions>()
                        .Configure(configure)
                        .ValidateDataAnnotations()
                        .Services

                        // add implementation
                        .AddSingleton<BinanceUsageContext>()
                        .AddSingleton<BinanceTradingService>()
                        .AddSingleton<ITradingService, BinanceTradingService>(sp => sp.GetRequiredService<BinanceTradingService>())
                        .AddSingleton<IHostedService, BinanceTradingService>(sp => sp.GetRequiredService<BinanceTradingService>())
                        .AddSingleton<BinanceApiConcurrencyHandler>()
                        .AddSingleton<BinanceApiCircuitBreakerHandler>()
                        .AddSingleton<BinanceApiSigningPreHandler>()
                        .AddSingleton<BinanceApiErrorPostHandler>()
                        .AddSingleton<BinanceApiUsagePostHandler>()
                        .AddSingleton<ISigner, Signer>()
                        .AddSingleton<IUserDataStreamClientFactory, BinanceUserDataStreamWssClientFactory>()
                        .AddSingleton<IMarketDataStreamClientFactory, BinanceMarketDataStreamWssClientFactory>()
                        .AddSingleton<ITickerProvider, BinanceTickerProvider>()
                        .AddSingleton<IKlineProvider, BinanceKlineProvider>()
                        .AddSingleton<ISavingsProvider, BinanceSavingsProvider>()

                        // add typed http client
                        .AddHttpClient<BinanceApiClient>((p, x) =>
                        {
                            var options = p.GetRequiredService<IOptions<BinanceOptions>>().Value;

                            x.BaseAddress = options.BaseApiAddress;
                            x.Timeout = options.Timeout;
                        })
                        .AddHttpMessageHandler<BinanceApiConcurrencyHandler>()
                        .AddHttpMessageHandler<BinanceApiCircuitBreakerHandler>()
                        .AddHttpMessageHandler<BinanceApiSigningPreHandler>()
                        .AddHttpMessageHandler<BinanceApiErrorPostHandler>()
                        .AddHttpMessageHandler<BinanceApiUsagePostHandler>()
                        .Services

                        // add auto mapper
                        .AddAutoMapper(options =>
                        {
                            options.AddProfile<BinanceAutoMapperProfile>();
                        })
                        .AddSingleton<ServerTimeConverter>()
                        .AddSingleton<TimeZoneInfoConverter>()
                        .AddSingleton<DateTimeConverter>()
                        .AddSingleton<RateLimitConverter>()
                        .AddSingleton<SymbolStatusConverter>()
                        .AddSingleton<OrderTypeConverter>()
                        .AddSingleton<SymbolFilterConverter>()
                        .AddSingleton<PermissionConverter>()
                        .AddSingleton<OrderSideConverter>()
                        .AddSingleton<TimeInForceConverter>()
                        .AddSingleton<NewOrderResponseTypeConverter>()
                        .AddSingleton<OrderStatusConverter>()
                        .AddSingleton<ContingencyTypeConverter>()
                        .AddSingleton<OcoStatusConverter>()
                        .AddSingleton<OcoOrderStatusConverter>()
                        .AddSingleton<CancelAllOrdersResponseModelConverter>()
                        .AddSingleton<AccountTypeConverter>()
                        .AddSingleton<UserDataStreamMessageConverter>()
                        .AddSingleton<ExecutionTypeConverter>()
                        .AddSingleton<MarketDataStreamMessageConverter>()
                        .AddSingleton<KlineIntervalConverter>()
                        .AddSingleton<FlexibleProductRedemptionTypeConverter>()
                        .AddSingleton<FlexibleProductStatusConverter>()
                        .AddSingleton<FlexibleProductFeaturedConverter>()
                        .AddSingleton(typeof(ImmutableListConverter<,>)) // todo: move this to the shared model converters

                        // add watchdog entries
                        .AddGrainWatchdogEntry(factory => factory.GetBinanceMarketDataGrain())
                        .AddGrainWatchdogEntry(factory => factory.GetBinanceUserDataGrain())

                        // add readyness entries
                        .AddReadynessEntry(sp => sp.GetRequiredService<IGrainFactory>().GetBinanceUserDataGrain().IsReadyAsync())
                        .AddReadynessEntry(sp => sp.GetRequiredService<IGrainFactory>().GetBinanceMarketDataGrain().IsReadyAsync());

                    // add object pool
                    services.TryAddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
                    services.TryAddSingleton(sp => sp.GetRequiredService<ObjectPoolProvider>().CreateStringBuilderPool());
                });

            return builder;
        }
    }
}