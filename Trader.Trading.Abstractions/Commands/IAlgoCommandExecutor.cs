﻿using Outcompute.Trader.Trading.Algorithms;
using System.Threading;
using System.Threading.Tasks;

namespace Outcompute.Trader.Trading.Commands
{
    public interface IAlgoCommandExecutor<in TAlgoCommand>
        where TAlgoCommand : notnull, IAlgoCommand
    {
        Task ExecuteAsync(IAlgoContext context, TAlgoCommand command, CancellationToken cancellationToken = default);
    }

    public interface IAlgoCommandExecutor<in TAlgoCommand, TResult>
        where TAlgoCommand : notnull, IAlgoCommand
    {
        Task<TResult> ExecuteAsync(IAlgoContext context, TAlgoCommand command, CancellationToken cancellationToken = default);
    }
}