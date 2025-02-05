﻿using Microsoft.Extensions.DependencyInjection;
using Outcompute.Trader.Trading.Algorithms.Context;
using Outcompute.Trader.Trading.Providers;

namespace Outcompute.Trader.Trading.Commands.RedeemSavings;

public class RedeemSavingsCommand : IAlgoCommand
{
    public RedeemSavingsCommand(string asset, decimal amount)
    {
        Guard.IsNotNull(asset, nameof(asset));

        Asset = asset;
        Amount = amount;
    }

    public string Asset { get; }
    public decimal Amount { get; }

    public ValueTask<RedeemSavingsEvent> ExecuteAsync(IAlgoContext context, CancellationToken cancellationToken = default)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        return context.ServiceProvider
            .GetRequiredService<IAlgoCommandExecutor<RedeemSavingsCommand, RedeemSavingsEvent>>()
            .ExecuteAsync(context, this, cancellationToken);
    }

    async ValueTask IAlgoCommand.ExecuteAsync(IAlgoContext context, CancellationToken cancellationToken)
    {
        await ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
    }
}