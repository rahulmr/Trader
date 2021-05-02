﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Trader.Core.Time;
using Trader.Core.Timers;
using Trader.Models;
using Trader.Trading.Algorithms;

namespace Trader.Trading
{
    internal class TradingHost : IHostedService
    {
        private readonly ILogger _logger;
        private readonly IEnumerable<ITradingAlgorithm> _algos;
        private readonly ISafeTimerFactory _timers;
        private readonly ITradingService _trader;
        private readonly ISystemClock _clock;

        public TradingHost(ILogger<TradingHost> logger, IEnumerable<ITradingAlgorithm> algos, ISafeTimerFactory timers, ITradingService trader, ISystemClock clock)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _algos = algos ?? throw new ArgumentNullException(nameof(algos));
            _timers = timers ?? throw new ArgumentNullException(nameof(timers));
            _trader = trader ?? throw new ArgumentNullException(nameof(trader));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        private static string Name => nameof(TradingHost);

        private ISafeTimer? _timer;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = _timers.Create(TickAsync, TimeSpan.Zero, TimeSpan.FromSeconds(10), Debugger.IsAttached ? TimeSpan.FromHours(1) : TimeSpan.FromMinutes(3));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Dispose();

            return Task.CompletedTask;
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Timer")]
        private async Task TickAsync(CancellationToken cancellationToken)
        {
            // grab the exchange information once to share between all algo instances
            var exchangeInfo = await _trader
                .GetExchangeInfoAsync(cancellationToken)
                .ConfigureAwait(false);

            var accountInfo = await _trader
                .GetAccountInfoAsync(new GetAccountInfo(null, _clock.UtcNow), cancellationToken)
                .ConfigureAwait(false);

            // if debugging execute all algos in sequence for ease of troubleshooting
            if (Debugger.IsAttached)
            {
                foreach (var algo in _algos)
                {
                    try
                    {
                        await algo
                            .GoAsync(exchangeInfo, accountInfo, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "{Name} reports {Symbol} algorithm has faulted",
                            Name, algo.Symbol);
                    }
                }
            }
            // otherwise execute all algos in parallel for max performance
            else
            {
                var tasks = new List<(string Symbol, Task Task)>();
                foreach (var algo in _algos)
                {
                    tasks.Add((algo.Symbol, algo.GoAsync(exchangeInfo, accountInfo, cancellationToken)));
                }

                foreach (var item in tasks)
                {
                    try
                    {
                        await item.Task.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "{Name} reports {Symbol} algorithm has faulted",
                            Name, item.Symbol);
                    }
                }
            }

            var profits = new List<(string Symbol, Profit Profit, Statistics Stats)>();
            foreach (var algo in _algos)
            {
                var profit = await algo
                    .GetProfitAsync(cancellationToken)
                    .ConfigureAwait(false);

                var stats = await algo
                    .GetStatisticsAsync(cancellationToken)
                    .ConfigureAwait(false);

                profits.Add((algo.Symbol, profit, stats));
            }

            foreach (var group in profits.GroupBy(x => x.Profit.Quote).OrderBy(x => x.Key))
            {
                _logger.LogInformation(
                    "{Name} reporting profit for quote {Quote}...",
                    Name, group.Key);

                foreach (var item in group.OrderBy(x => x.Symbol))
                {
                    _logger.LogInformation(
                        "{Name} reports {Symbol,8} profit as (T: {@Today,12:F8}, T-1: {@Yesterday,12:F8}, W: {@ThisWeek,12:F8}, W-1: {@PrevWeek,12:F8}, M: {@ThisMonth,12:F8}, Y: {@ThisYear,13:F8}) (APD1: {@AveragePerDay1,12:F8}, APD7: {@AveragePerDay7,12:F8}, APD30: {@AveragePerDay30,12:F8})",
                        Name, item.Symbol, item.Profit.Today, item.Profit.Yesterday, item.Profit.ThisWeek, item.Profit.PrevWeek, item.Profit.ThisMonth, item.Profit.ThisYear, item.Stats.AvgPerDay1, item.Stats.AvgPerDay7, item.Stats.AvgPerDay30);
                }

                var totalProfit = Profit.Aggregate(group.Select(x => x.Profit));
                var totalStats = Statistics.FromProfit(totalProfit);

                _logger.LogInformation(
                    "{Name} reports {Quote,8} profit as (T: {@Today,12:F8}, T-1: {@Yesterday,12:F8}, W: {@ThisWeek,12:F8}, W-1: {@PrevWeek,12:F8}, M: {@ThisMonth,12:F8}, Y: {@ThisYear,13:F8}) (APD1: {@AveragePerDay1,12:F8}, APD7: {@AveragePerDay7,12:F8}, APD30: {@AveragePerDay30,12:F8})",
                    Name,
                    group.Key,
                    totalProfit.Today,
                    totalProfit.Yesterday,
                    totalProfit.ThisWeek,
                    totalProfit.PrevWeek,
                    totalProfit.ThisMonth,
                    totalProfit.ThisYear,
                    totalStats.AvgPerDay1,
                    totalStats.AvgPerDay7,
                    totalStats.AvgPerDay30);
            }
        }
    }
}