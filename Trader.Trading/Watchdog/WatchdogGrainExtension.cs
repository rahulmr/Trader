﻿namespace Outcompute.Trader.Trading.Watchdog;

internal class WatchdogGrainExtension : IWatchdogGrainExtension
{
    public Task PingAsync()
    {
        return Task.CompletedTask;
    }
}