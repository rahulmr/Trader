﻿using Outcompute.Trader.Models;

namespace Outcompute.Trader.Trading.Providers;

public static class SavingsProviderExtensions
{
    public static Task<SavingsPosition> GetPositionOrZeroAsync(this ISavingsProvider provider, string asset, CancellationToken cancellationToken = default)
    {
        if (provider is null) throw new ArgumentNullException(nameof(provider));
        if (asset is null) throw new ArgumentNullException(nameof(asset));

        return GetPositionOrZeroCoreAsync(provider, asset, cancellationToken);

        async static Task<SavingsPosition> GetPositionOrZeroCoreAsync(ISavingsProvider provider, string asset, CancellationToken cancellationToken)
        {
            return await provider.TryGetPositionAsync(asset, cancellationToken).ConfigureAwait(false)
                ?? SavingsPosition.Zero(asset);
        }
    }

    public static Task<SavingsQuota> GetQuotaOrZeroAsync(this ISavingsProvider provider, string asset, CancellationToken cancellationToken = default)
    {
        if (provider is null) throw new ArgumentNullException(nameof(provider));
        if (asset is null) throw new ArgumentNullException(nameof(asset));

        return GetQuotaOrZeroCoreAsync(provider, asset, cancellationToken);

        async static Task<SavingsQuota> GetQuotaOrZeroCoreAsync(ISavingsProvider provider, string asset, CancellationToken cancellationToken)
        {
            return await provider.TryGetQuotaAsync(asset, cancellationToken).ConfigureAwait(false)
                ?? SavingsQuota.Zero(asset);
        }
    }
}