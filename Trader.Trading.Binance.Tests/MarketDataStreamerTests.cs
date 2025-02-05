﻿using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Outcompute.Trader.Models;
using Outcompute.Trader.Trading.Binance.Providers.MarketData;
using Outcompute.Trader.Trading.Providers;

namespace Outcompute.Trader.Trading.Binance.Tests
{
    public class MarketDataStreamerTests
    {
        [Fact]
        public async Task Streams()
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(1));

            // arrange
            var logger = NullLogger<MarketDataStreamer>.Instance;
            var mapper = Mock.Of<IMapper>();
            var symbol = "ABCXYZ";

            var ticker = MiniTicker.Empty with { Symbol = symbol, ClosePrice = 123m };
            var kline = Kline.Empty with { Symbol = symbol, Interval = KlineInterval.Days1, ClosePrice = 123m };

            var client = Mock.Of<IMarketDataStreamClient>();
            Mock.Get(client)
                .SetupSequence(x => x.ReceiveAsync(cancellation.Token))
                .Returns(Task.FromResult(new MarketDataStreamMessage(null, ticker, null, null)))
                .Returns(Task.FromResult(new MarketDataStreamMessage(null, null, kline, null)))
                .Returns(Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token).ContinueWith(x => MarketDataStreamMessage.Empty, cancellation.Token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default));

            var factory = Mock.Of<IMarketDataStreamClientFactory>();
            Mock.Get(factory)
                .Setup(x => x.Create(It.IsAny<IReadOnlyCollection<string>>()))
                .Returns(client)
                .Verifiable();

            var receivedTicker = new TaskCompletionSource();
            using var reg1 = cancellation.Token.Register(() => receivedTicker.TrySetCanceled());

            var tickerProvider = Mock.Of<ITickerProvider>();
            Mock.Get(tickerProvider)
                .Setup(x => x.ConflateTickerAsync(ticker, cancellation.Token))
                .Callback(() => receivedTicker.TrySetResult())
                .Returns(ValueTask.CompletedTask)
                .Verifiable();

            var receivedKline = new TaskCompletionSource();
            using var reg2 = cancellation.Token.Register(() => receivedKline.TrySetCanceled());

            var klineProvider = Mock.Of<IKlineProvider>();
            Mock.Get(klineProvider)
                .Setup(x => x.ConflateKlineAsync(kline, cancellation.Token))
                .Callback(() => receivedKline.TrySetResult())
                .Returns(ValueTask.CompletedTask)
                .Verifiable();

            var streamer = new MarketDataStreamer(logger, mapper, factory, tickerProvider, klineProvider);
            var tickers = new HashSet<string>(new[] { symbol });
            var klines = new HashSet<(string, KlineInterval)>(new[] { (symbol, KlineInterval.Days1) });

            // act - start streaming
            var task = streamer.StartAsync(tickers, klines, cancellation.Token);
            await receivedTicker.Task;
            await receivedKline.Task;

            // assert
            Mock.Get(tickerProvider).Verify(x => x.ConflateTickerAsync(ticker, cancellation.Token));
            Mock.Get(klineProvider).Verify(x => x.ConflateKlineAsync(kline, cancellation.Token));
            Mock.Get(factory).VerifyAll();

            cancellation.Cancel();
            await Assert.ThrowsAsync<TaskCanceledException>(() => task);
        }
    }
}