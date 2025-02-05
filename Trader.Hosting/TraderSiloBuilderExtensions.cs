﻿using Microsoft.Extensions.DependencyInjection;

namespace Orleans.Hosting;

public static class TraderSiloBuilderExtensions
{
    public static ISiloBuilder AddTrader(this ISiloBuilder builder)
    {
        return builder
            .AddTradingServices()
            .ConfigureServices((context, services) =>
            {
                services
                    .AddModelServices()
                    .AddTraderCoreServices();
            });
    }
}