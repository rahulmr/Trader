﻿using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using System;
using Trader.Trading;
using Trader.Trading.Binance;
using Trader.Trading.Binance.Converters;
using Trader.Trading.Binance.Signing;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class BinanceTradingServiceCollectionExtensions
    {
        public static IServiceCollection AddBinanceTradingService(this IServiceCollection services, Action<BinanceOptions> configure)
        {
            services

                // add implementation
                .AddSingleton<BinanceTradingService>()
                .AddSingleton<ITradingService, BinanceTradingService>(sp => sp.GetRequiredService<BinanceTradingService>())
                .AddSingleton<IHostedService, BinanceTradingService>(sp => sp.GetRequiredService<BinanceTradingService>())
                .AddSingleton<BinanceApiHandler>()
                .AddSingleton<ISigner, Signer>()
                .AddSingleton<IUserDataStreamClient, BinanceUserDataStreamWssClient>()

                // add options
                .AddOptions<BinanceOptions>()
                .Configure(configure)
                .ValidateDataAnnotations()
                .Services

                // add typed http client
                .AddHttpClient<BinanceApiClient>((p, x) =>
                {
                    var options = p.GetRequiredService<IOptions<BinanceOptions>>().Value;

                    x.BaseAddress = options.BaseApiAddress;
                    x.Timeout = options.Timeout;
                })
                .AddHttpMessageHandler<BinanceApiHandler>()
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
                .AddSingleton(typeof(ImmutableListConverter<,>)); // todo: move this to the shared model converters

            // add object pool
            services.TryAddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
            services.TryAddSingleton(sp => sp.GetRequiredService<ObjectPoolProvider>().CreateStringBuilderPool());

            return services;
        }
    }
}