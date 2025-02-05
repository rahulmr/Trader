﻿namespace Outcompute.Trader.Trading.Providers.Klines;

internal interface IKlineProviderGrain : IGrainWithStringKey
{
    Task SetLastSyncedKlineOpenTimeAsync(DateTime time);

    Task<DateTime> GetLastSyncedKlineOpenTimeAsync();

    ValueTask<ReactiveResult> GetKlinesAsync();

    ValueTask<ReactiveResult?> TryWaitForKlinesAsync(Guid version, int fromSerial);

    ValueTask<Kline?> TryGetKlineAsync(DateTime openTime);

    ValueTask SetKlineAsync(Kline item);

    ValueTask SetKlinesAsync(IEnumerable<Kline> items);
}