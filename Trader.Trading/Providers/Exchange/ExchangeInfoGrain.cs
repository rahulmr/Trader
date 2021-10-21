﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Concurrency;
using Outcompute.Trader.Models;
using System;
using System.Threading.Tasks;

namespace Outcompute.Trader.Trading.Providers.Exchange
{
    [Reentrant]
    internal class ExchangeInfoGrain : Grain, IExchangeInfoGrain
    {
        private readonly ExchangeInfoOptions _options;
        private readonly ITradingService _trader;
        private readonly IHostApplicationLifetime _lifetime;

        public ExchangeInfoGrain(IOptions<ExchangeInfoOptions> options, ITradingService trader, IHostApplicationLifetime lifetime)
        {
            _options = options.Value;
            _trader = trader;
            _lifetime = lifetime;
        }

        private ExchangeInfo _info = ExchangeInfo.Empty;
        private Guid _version = Guid.NewGuid();

        public override async Task OnActivateAsync()
        {
            await Refresh();

            RegisterTimer(_ => Refresh(), null, _options.RefreshPeriod, _options.RefreshPeriod);

            await base.OnActivateAsync();
        }

        private async Task Refresh()
        {
            _info = await _trader.GetExchangeInfoAsync(_lifetime.ApplicationStopping);

            _version = Guid.NewGuid();
        }

        /// <summary>
        /// Gets the current exchange info and version tag.
        /// </summary>
        public Task<ExchangeInfoResult> GetExchangeInfoAsync()
        {
            return Task.FromResult(new ExchangeInfoResult(_info, _version));
        }

        /// <summary>
        /// Gets the current exchange info and version if the client version differs.
        /// Otherwise returns null exchange info with the same version.
        /// </summary>
        public Task<ExchangeInfoTryResult> TryGetExchangeInfoAsync(Guid version)
        {
            // if the client has the latest version then return nothing
            if (version == _version)
            {
                return Task.FromResult(new ExchangeInfoTryResult(null, version));
            }

            // otherwise return the latest version
            return Task.FromResult(new ExchangeInfoTryResult(_info, _version));
        }
    }
}