﻿using Orleans.Concurrency;
using Outcompute.Trader.Models;
using System;
using System.Collections.Generic;

namespace Outcompute.Trader.Trading.Providers.Klines
{
    [Immutable]
    internal readonly record struct ReactiveResult(Guid Version, int Serial, IReadOnlyList<Kline> Items);
}