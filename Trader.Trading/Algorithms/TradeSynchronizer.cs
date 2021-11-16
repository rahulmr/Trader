﻿using Microsoft.Extensions.Logging;
using Outcompute.Trader.Models;
using Outcompute.Trader.Trading.Providers;
using System.Diagnostics;
using System.Threading.Tasks.Dataflow;

namespace Outcompute.Trader.Trading.Algorithms;

internal partial class TradeSynchronizer : ITradeSynchronizer
{
    private readonly ILogger _logger;
    private readonly ITradingService _trader;
    private readonly ITradeProvider _provider;

    public TradeSynchronizer(ILogger<TradeSynchronizer> logger, ITradingService trader, ITradeProvider provider)
    {
        _logger = logger;
        _trader = trader;
        _provider = provider;
    }

    private static string TypeName { get; } = nameof(TradeSynchronizer);

    public async Task SynchronizeTradesAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();

        // start from the last trade if possible
        var tradeId = (await _provider.TryGetLastTradeIdAsync(symbol, cancellationToken).ConfigureAwait(false) ?? 0) + 1;

        // save all trades in the background so we can keep pulling trades
        var worker = new ActionBlock<IEnumerable<AccountTrade>>(work => _provider.SetTradesAsync(symbol, work, cancellationToken));

        // pull all trades
        var count = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            // query for the next trades
            var trades = await _trader
                .WithBackoff()
                .GetAccountTradesAsync(symbol, tradeId, 1000, cancellationToken)
                .ConfigureAwait(false);

            // break if we got all trades
            if (trades.Count is 0) break;

            // persist all new trades in this page
            worker.Post(trades);

            // keep the last trade
            tradeId = trades.Max!.Id + 1;

            // keep track for logging
            count += trades.Count;
        }

        worker.Complete();
        await worker.Completion;

        LogPulledTrades(TypeName, symbol, count, tradeId, watch.ElapsedMilliseconds);
    }

    #region Logging

    [LoggerMessage(0, LogLevel.Information, "{Type} {Symbol} pulled {Count} trades up to TradeId {MaxTradeId} in {ElapsedMs}ms")]
    private partial void LogPulledTrades(string type, string symbol, int count, long maxTradeId, long elapsedMs);

    #endregion Logging
}