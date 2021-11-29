﻿using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Concurrency;
using Outcompute.Trader.Core.Tasks.Dataflow;
using Outcompute.Trader.Models;
using Outcompute.Trader.Trading.Providers;
using System.Collections.Immutable;

namespace Outcompute.Trader.Trading.Binance.Providers.MarketData
{
    // todo: promote this to common implementation and hide it behind the ticker provider
    [Reentrant]
    [StatelessWorker(1)]
    internal class KlineConflaterGrain : Grain, IKlineConflaterGrain
    {
        private readonly IKlineProvider _provider;
        private readonly IHostApplicationLifetime _lifetime;

        public KlineConflaterGrain(IKlineProvider provider, IHostApplicationLifetime lifetime)
        {
            _provider = provider;
            _lifetime = lifetime;
        }

        private string _symbol = Empty;

        private KlineInterval _interval = KlineInterval.None;

        private readonly ConflateChannel<Kline, ImmutableHashSet<Kline>.Builder, IEnumerable<Kline>> _channel = new(
            () => ImmutableHashSet.CreateBuilder(KlineComparer.Key),
            (agg, item) =>
            {
                if (!agg.TryGetValue(item, out var current))
                {
                    agg.Add(item);
                }
                else if (item.EventTime >= current.EventTime)
                {
                    agg.Remove(current);
                    agg.Add(item);
                }

                return agg;
            },
            agg => agg.ToImmutable());

        private Task? _stream;

        public override Task OnActivateAsync()
        {
            var keys = this.GetPrimaryKeyString().Split('|');
            _symbol = keys[0];
            _interval = Enum.Parse<KlineInterval>(keys[1]);

            RegisterTimer(_ => MonitorAsync(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

            return base.OnActivateAsync();
        }

        private async Task MonitorAsync()
        {
            if (_lifetime.ApplicationStopping.IsCancellationRequested)
            {
                return;
            }

            if (_stream is null)
            {
                _stream = Task.Run(StreamAsync);
                return;
            }

            if (_stream.IsCompleted)
            {
                try
                {
                    await _stream;
                }
                catch (OperationCanceledException)
                {
                    // noop
                }
                finally
                {
                    _stream = null;
                }
            }
        }

        private async Task StreamAsync()
        {
            await foreach (var result in _channel.Reader.ReadAllAsync(_lifetime.ApplicationStopping))
            {
                await _provider.SetKlinesAsync(_symbol, _interval, result, _lifetime.ApplicationStopping);
            }
        }

        public ValueTask PushAsync(Kline item)
        {
            return _channel.Writer.WriteAsync(item, _lifetime.ApplicationStopping);
        }
    }
}