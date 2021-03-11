﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Trader.Core.Time;
using Trader.Core.Timers;
using Trader.Core.Trading.Algorithms;
using Trader.Core.Trading.Algorithms.Step;

namespace Trader.Core.Trading
{
    internal class TradingHost : ITradingHost, IHostedService
    {
        private readonly ILogger _logger;
        private readonly IStepAlgorithmFactory _factory;
        private readonly ISafeTimer _timer;
        private readonly ITradingService _trader;
        private readonly ISystemClock _clock;

        public TradingHost(ILogger<TradingHost> logger, IStepAlgorithmFactory factory, ISafeTimerFactory timerFactory, ITradingService trader, ISystemClock clock)
        {
            _logger = logger;
            _factory = factory;
            _trader = trader;
            _clock = clock;

            _timer = timerFactory.Create(_ => TickAsync(), TimeSpan.Zero, TimeSpan.FromSeconds(10));

            _algos.Add(_factory.Create("BTCGBP-1"));
            _algos.Add(_factory.Create("ETHGBP-1"));
            _algos.Add(_factory.Create("ADAGBP-1"));
            _algos.Add(_factory.Create("XRPGBP-1"));
            _algos.Add(_factory.Create("LINKGBP-1"));
            _algos.Add(_factory.Create("DOGEGBP-1"));
            _algos.Add(_factory.Create("SXPGBP-1"));
            _algos.Add(_factory.Create("DOTGBP-1"));
        }

        private static string Name => nameof(TradingHost);

        private readonly List<ITradingAlgorithm> _algos = new();

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _timer.StartAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _timer.StopAsync(cancellationToken);
        }

        private async Task TickAsync()
        {
            // grab the exchange information once to share between all algo instances
            var exchange = await _trader.GetExchangeInfoAsync();
            var accountInfo = await _trader.GetAccountInfoAsync(new GetAccountInfo(null, _clock.UtcNow));

            var tasks = new List<Task>(_algos.Count);

            foreach (var algo in _algos)
            {
                tasks.Add(algo switch
                {
                    IStepAlgorithm step => step.GoAsync(exchange, accountInfo),
                    _ => throw new NotSupportedException($"Unknown Algorithm '{algo.GetType().FullName}'"),
                });
            }

            await Task.WhenAll(tasks);
        }
    }
}