﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Outcompute.Trader.Models;
using Outcompute.Trader.Trading.Commands;
using Outcompute.Trader.Trading.Providers;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Outcompute.Trader.Trading.Algorithms.Standard.Discovery
{
    internal class DiscoveryAlgo : Algo
    {
        private readonly IOptionsMonitor<DiscoveryAlgoOptions> _monitor;
        private readonly ILogger _logger;
        private readonly ITradingService _trader;
        private readonly IAlgoDependencyResolver _dependencies;
        private readonly IExchangeInfoProvider _info;
        private readonly ISwapPoolProvider _swaps;

        public DiscoveryAlgo(IOptionsMonitor<DiscoveryAlgoOptions> monitor, ILogger<DiscoveryAlgo> logger, ITradingService trader, IAlgoDependencyResolver dependencies, IExchangeInfoProvider info, ISwapPoolProvider swaps)
        {
            _monitor = monitor;
            _logger = logger;
            _trader = trader;
            _dependencies = dependencies;
            _info = info;
            _swaps = swaps;
        }

        public override async Task<IAlgoCommand> GoAsync(CancellationToken cancellationToken = default)
        {
            var options = _monitor.Get(Context.Name);

            // get the exchange info
            var symbols = (await _info.GetExchangeInfoAsync(cancellationToken).ConfigureAwait(false)).Symbols
                .Where(x => x.Status == SymbolStatus.Trading)
                .Where(x => x.IsSpotTradingAllowed)
                .Where(x => options.QuoteAssets.Contains(x.QuoteAsset))
                .Where(x => !options.IgnoreSymbols.Contains(x.Name))
                .ToList();

            // get all usable savings assets
            var assets = (await _trader.GetSubscribableSavingsProductsAsync(cancellationToken).ConfigureAwait(false))
                .Where(x => x.CanPurchase)
                .Select(x => x.Asset)
                .Union(options.ForcedAssets)
                .ToHashSet();

            // get all usable swap pools
            var pools = await _swaps.GetSwapPoolsAsync(cancellationToken);

            // identify all symbols with savings
            var withSavings = symbols
                .Where(x => assets.Contains(x.QuoteAsset) && assets.Contains(x.BaseAsset))
                .Select(x => x.Name)
                .ToHashSet();

            // identify all symbols with swap pools
            var withSwapPools = symbols
                .Where(x => pools.Any(p => p.Assets.Contains(x.QuoteAsset) && p.Assets.Contains(x.BaseAsset)))
                .Select(x => x.Name)
                .ToHashSet();

            // get all symbols in use
            var used = _dependencies.AllSymbols;

            // identify unused symbols with savings
            var unusedWithSavings = withSavings
                .Except(used)
                .ToList();

            _logger.LogInformation(
                "{Type} identified {Count} unused symbols with savings: {Symbols}",
                nameof(DiscoveryAlgo), unusedWithSavings.Count, unusedWithSavings);

            // identify unused symbols with swap pools
            var unusedWithSwapPools = withSwapPools
                .Except(used)
                .ToList();

            _logger.LogInformation(
                "{Type} identified {Count} unused symbols with swap pools: {Symbols}",
                nameof(DiscoveryAlgo), unusedWithSwapPools.Count, unusedWithSwapPools);

            // identify used symbols without savings or swap pools
            var risky = used
                .Except(options.IgnoreSymbols)
                .Except(withSavings)
                .Except(withSwapPools)
                .ToList();

            _logger.LogWarning(
                "{Type} identified {Count} used symbols without savings or swap pools: {Symbols}",
                nameof(DiscoveryAlgo), risky.Count, risky);

            return Noop();
        }
    }
}