﻿using Outcompute.Trader.Models;
using Outcompute.Trader.Trading.Providers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Outcompute.Trader.Trading.Algorithms
{
    public interface IAlgoContext
    {
        /// <summary>
        /// The current algorithm name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The tick time of the last update.
        /// </summary>
        DateTime TickTime { get; }

        /// <summary>
        /// The service provider for extension methods to use.
        /// </summary>
        IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// Provides full symbol information For algos that derive from <see cref="ISymbolAlgo"/>.
        /// </summary>
        Symbol Symbol { get; }

        /// <summary>
        /// The current significant asset information for the default symbol.
        /// This is only populated if the default symbol is defined.
        /// </summary>
        PositionDetails PositionDetails { get; }

        /// <summary>
        /// The current ticker for the default symbol.
        /// This is only populated if the default symbol is defined.
        /// </summary>
        MiniTicker Ticker { get; }

        /// <summary>
        /// The current spot balance for the base asset of the default symbol.
        /// This is only populated if the default symbol is defined.
        /// </summary>
        Balance AssetSpotBalance { get; }

        /// <summary>
        /// The current spot balance for the quote asset of the default symbol.
        /// This is only populated if the default symbol is defined.
        /// </summary>
        Balance QuoteSpotBalance { get; }

        /// <summary>
        /// The current savings balance for the base asset of the default symbol.
        /// This is only populated if the default symbol is defined.
        /// </summary>
        SavingsPosition AssetSavingsBalance { get; }

        /// <summary>
        /// The current savings balance for the quote asset of the default symbol.
        /// This is only populated if the default symbol is defined.
        /// </summary>
        SavingsPosition QuoteSavingsBalance { get; }

        /// <summary>
        /// The current swap pool balances for the base asset of the default symbol.
        /// This is only populated if the default symbol is defined.
        /// </summary>
        SwapPoolAssetBalance AssetSwapPoolBalance { get; }

        /// <summary>
        /// The current swap pool balances for the quote asset of the default symbol.
        /// This is only populated if the default symbol is defined.
        /// </summary>
        SwapPoolAssetBalance QuoteSwapPoolBalance { get; }

        /// <summary>
        /// Gets all historial orders for the default symbol.
        /// This is only populated if the default symbol is defined.
        /// </summary>
        IReadOnlyList<OrderQueryResult> Orders { get; }

        /// <summary>
        /// Gets the klines for the configured dependencies.
        /// </summary>
        IDictionary<(string Symbol, KlineInterval Interval), IEnumerable<Kline>> Klines { get; }

        /// <summary>
        /// Makes the context self-update to the latest data.
        /// </summary>
        ValueTask UpdateAsync(CancellationToken cancellationToken = default);
    }
}