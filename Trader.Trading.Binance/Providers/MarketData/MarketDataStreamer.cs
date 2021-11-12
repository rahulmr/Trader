﻿using AutoMapper;
using Microsoft.Extensions.Logging;
using Outcompute.Trader.Models;
using Outcompute.Trader.Trading.Providers;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Outcompute.Trader.Trading.Binance.Providers.MarketData
{
    internal class MarketDataStreamer : IMarketDataStreamer
    {
        private readonly ILogger _logger;
        private readonly IMapper _mapper;
        private readonly IMarketDataStreamClientFactory _factory;
        private readonly IKlineProvider _klines;
        private readonly ITickerProvider _tickers;

        public MarketDataStreamer(ILogger<MarketDataStreamer> logger, IMapper mapper, IMarketDataStreamClientFactory factory, IKlineProvider klines, ITickerProvider tickers)
        {
            _logger = logger;
            _mapper = mapper;
            _factory = factory;
            _klines = klines;
            _tickers = tickers;
        }

        private static string TypeName => nameof(MarketDataStreamer);

        public Task StreamAsync(IEnumerable<string> tickers, IEnumerable<(string Symbol, KlineInterval Interval)> klines, CancellationToken cancellationToken = default)
        {
            return StreamCoreAsync(tickers, klines, cancellationToken);
        }

        [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "N/A")]
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Worker")]
        private async Task StreamCoreAsync(IEnumerable<string> tickers, IEnumerable<(string Symbol, KlineInterval Interval)> klines, CancellationToken cancellationToken)
        {
            var tickerLookup = tickers.ToHashSet();
            var klineLookup = klines.ToHashSet();

            // create a client for the streams we want
            var streams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            streams.UnionWith(tickerLookup.Select(x => $"{x.ToLowerInvariant()}@miniTicker"));
            streams.UnionWith(klineLookup.Select(x => $"{x.Symbol.ToLowerInvariant()}@kline_{_mapper.Map<string>(x.Interval)}"));

            _logger.LogInformation(
                "{Name} connecting to streams {Streams}...",
                TypeName, streams);

            using var client = _factory.Create(streams);

            await client
                .ConnectAsync(cancellationToken)
                .ConfigureAwait(false);

            // this worker action pushes incoming klines to the system in the background so we dont hold up the binance stream
            var klineWorker = new ActionBlock<Kline>(async item =>
            {
                try
                {
                    await _klines
                        .SetKlineAsync(item, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{Name} failed to push kline {Kline}", TypeName, item);
                }
            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = klineLookup.Count
            });

            // this worker action pushes incoming tickers to the system in the background so we dont hold up the binance stream
            var tickerWorker = new ActionBlock<MiniTicker>(async item =>
            {
                try
                {
                    await _tickers
                        .SetTickerAsync(item, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{Name} failed to push ticker {Ticker}", TypeName, item);
                }
            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = tickerLookup.Count
            });

            // now we can stream from the exchange
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await client
                    .ReceiveAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (message.Error is not null)
                {
                    throw new BinanceCodeException(message.Error.Code, message.Error.Message, 0);
                }

                if (message.MiniTicker is not null && tickerLookup.Contains(message.MiniTicker.Symbol))
                {
                    tickerWorker.Post(message.MiniTicker);
                }

                if (message.Kline.HasValue && klineLookup.Contains((message.Kline.Value.Symbol, message.Kline.Value.Interval)))
                {
                    klineWorker.Post(message.Kline.Value);
                }
            }
        }
    }
}