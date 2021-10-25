﻿using Outcompute.Trader.Trading.Algorithms;
using System.Threading;
using System.Threading.Tasks;

namespace Outcompute.Trader.Trading.Commands.Many
{
    internal class ManyExecutor : IAlgoCommandExecutor<ManyCommand>
    {
        public async Task ExecuteAsync(IAlgoContext context, ManyCommand command, CancellationToken cancellationToken = default)
        {
            foreach (var item in command.Results)
            {
                await item
                    .ExecuteAsync(context, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }
}