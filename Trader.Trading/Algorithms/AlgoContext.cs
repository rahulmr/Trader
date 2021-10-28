﻿using Outcompute.Trader.Core;
using Outcompute.Trader.Models;
using System;
using System.Threading;

namespace Outcompute.Trader.Trading.Algorithms
{
    internal class AlgoContext : IAlgoContext
    {
        public AlgoContext(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public string Name { get; internal set; } = string.Empty;

        public Symbol Symbol { get; internal set; } = Symbol.Empty;

        public IServiceProvider ServiceProvider { get; }

        public SignificantResult Significant { get; set; } = SignificantResult.Empty;

        public MiniTicker Ticker { get; set; } = MiniTicker.Empty;

        public Balance AssetSpotBalance { get; set; } = Balance.Empty;

        public Balance QuoteSpotBalance { get; set; } = Balance.Empty;

        public SavingsPosition AssetSavingsBalance { get; set; } = SavingsPosition.Empty;

        public SavingsPosition QuoteSavingsBalance { get; set; } = SavingsPosition.Empty;

        #region Static Helpers

        public static AlgoContext Empty { get; } = new AlgoContext(NullServiceProvider.Instance);

        private static readonly AsyncLocal<AlgoContext> _current = new();

        internal static AlgoContext Current
        {
            get
            {
                return _current.Value ?? Empty;
            }
            set
            {
                _current.Value = value ?? throw new ArgumentNullException(nameof(value));
            }
        }

        #endregion Static Helpers
    }
}