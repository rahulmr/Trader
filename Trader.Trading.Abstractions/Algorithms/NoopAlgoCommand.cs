﻿using Outcompute.Trader.Trading.Algorithms.Context;
using Outcompute.Trader.Trading.Commands;

namespace Outcompute.Trader.Trading.Algorithms;

public class NoopAlgoCommand : IAlgoCommand
{
    private NoopAlgoCommand()
    {
    }

    public ValueTask ExecuteAsync(IAlgoContext context, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    public static NoopAlgoCommand Instance { get; } = new NoopAlgoCommand();
}