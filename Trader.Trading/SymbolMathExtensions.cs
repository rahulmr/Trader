﻿using System;

namespace Outcompute.Trader.Models
{
    public static class SymbolMathExtensions
    {
        public static decimal AdjustQuantityDownToLotSize(this decimal quantity, Symbol symbol)
        {
            if (symbol is null) throw new ArgumentNullException(nameof(symbol));

            return Math.Floor(quantity / symbol.Filters.LotSize.StepSize) * symbol.Filters.LotSize.StepSize;
        }

        public static decimal AdjustQuantityUpToLotSize(this decimal quantity, Symbol symbol)
        {
            if (symbol is null) throw new ArgumentNullException(nameof(symbol));

            return Math.Ceiling(quantity / symbol.Filters.LotSize.StepSize) * symbol.Filters.LotSize.StepSize;
        }

        public static decimal AdjustPriceDownToTickSize(this decimal price, Symbol symbol)
        {
            if (symbol is null) throw new ArgumentNullException(nameof(symbol));

            return Math.Floor(price / symbol.Filters.Price.TickSize) * symbol.Filters.Price.TickSize;
        }

        public static decimal AdjustPriceUpToTickSize(this decimal price, Symbol symbol)
        {
            if (symbol is null) throw new ArgumentNullException(nameof(symbol));

            return Math.Ceiling(price / symbol.Filters.Price.TickSize) * symbol.Filters.Price.TickSize;
        }

        public static decimal AdjustTotalUpToMinNotional(this decimal total, Symbol symbol)
        {
            if (symbol is null) throw new ArgumentNullException(nameof(symbol));

            return Math.Max(total, symbol.Filters.MinNotional.MinNotional);
        }
    }
}