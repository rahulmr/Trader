﻿using Orleans.Concurrency;
using Outcompute.Trader.Models;
using Outcompute.Trader.Models.Collections;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static System.String;

namespace Outcompute.Trader.Trading.Algorithms
{
    public interface ISignificantOrderResolver
    {
        Task<SignificantResult> ResolveAsync(Symbol symbol, CancellationToken cancellationToken = default);
    }

    [Immutable]
    public record SignificantResult(ImmutableSortedOrderSet Orders, ImmutableList<ProfitEvent> ProfitEvents, ImmutableList<CommissionEvent> CommissionEvents)
    {
        public static SignificantResult Empty { get; } = new SignificantResult(
            ImmutableSortedOrderSet.Empty,
            ImmutableList<ProfitEvent>.Empty,
            ImmutableList<CommissionEvent>.Empty);
    }

    [Immutable]
    public record Profit(

        // identifiers
        string Symbol,
        string Asset,
        string Quote,

        // fixed windows
        decimal Today, decimal Yesterday, decimal ThisWeek, decimal PrevWeek, decimal ThisMonth, decimal ThisYear, decimal All,

        // rolling windows
        decimal D1, decimal D7, decimal D30)
    {
        public static Profit Zero(string symbol, string asset, string quote)
        {
            return new Profit(symbol, asset, quote, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        public static Profit Aggregate(IEnumerable<Profit> items)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));

            var today = 0m;
            var yesterday = 0m;
            var thisWeek = 0m;
            var prevWeek = 0m;
            var thisMonth = 0m;
            var thisYear = 0m;
            var all = 0m;
            var d1 = 0m;
            var d7 = 0m;
            var d30 = 0m;

            string? quote = null;
            string? asset = null;
            string? symbol = null;
            var first = true;

            foreach (var item in items)
            {
                if (first)
                {
                    quote = item.Quote;
                    asset = item.Asset;
                    symbol = item.Symbol;

                    first = false;
                }
                else
                {
                    if (item.Quote != quote) throw new InvalidOperationException($"Cannot aggregate profit from different quotes '{quote}' and '{item.Quote}'");

                    if (item.Asset != asset) asset = null;
                    if (item.Symbol != symbol) symbol = null;
                }

                today += item.Today;
                yesterday += item.Yesterday;
                thisWeek += item.ThisWeek;
                prevWeek += item.PrevWeek;
                thisMonth += item.ThisMonth;
                thisYear += item.ThisYear;
                all += item.All;
                d1 += item.D1;
                d7 += item.D7;
                d30 += item.D30;
            }

            return new Profit(symbol ?? Empty, asset ?? Empty, quote ?? Empty, today, yesterday, thisWeek, prevWeek, thisMonth, thisYear, all, d1, d7, d30);
        }

        public Profit Add(Profit item)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));
            if (item.Quote != Quote) throw new InvalidOperationException($"Cannot aggregate profit from different quotes '{Quote}' and '{item.Quote}'");

            return new Profit(
                Symbol == item.Symbol ? Symbol : Empty,
                Asset == item.Asset ? Asset : Empty,
                Quote,
                Today + item.Today,
                Yesterday + item.Yesterday,
                ThisWeek + item.ThisWeek,
                PrevWeek + item.PrevWeek,
                ThisMonth + item.ThisMonth,
                ThisYear + item.ThisYear,
                All + item.All,
                D1 + item.D1,
                D7 + item.D7,
                D30 + item.D30);
        }

        public static Profit FromEvents(Symbol symbol, IEnumerable<ProfitEvent> profits, IEnumerable<CommissionEvent> commissions, DateTime now)
        {
            if (symbol is null) throw new ArgumentNullException(nameof(symbol));
            if (profits is null) throw new ArgumentNullException(nameof(profits));
            if (commissions is null) throw new ArgumentNullException(nameof(commissions));

            var lookup = commissions.ToLookup(x => (x.Asset, x.OrderId, x.TradeId));

            var todayProfit = 0m;
            var yesterdayProfit = 0m;
            var thisWeekProfit = 0m;
            var prevWeekProfit = 0m;
            var thisMonthProfit = 0m;
            var thisYearProfit = 0m;
            var all = 0m;
            var d1 = 0m;
            var d7 = 0m;
            var d30 = 0m;

            // hold the current time so profit assignments are consistent
            var window1d = now.AddDays(-1);
            var window7d = now.AddDays(-7);
            var window30d = now.AddDays(-30);
            var today = now.Date;

            foreach (var profit in profits)
            {
                // assign to the appropriate counters
                if (profit.EventTime.Date == today) todayProfit += profit.Profit;
                if (profit.EventTime.Date == today.AddDays(-1)) yesterdayProfit += profit.Profit;
                if (profit.EventTime.Date >= today.Previous(DayOfWeek.Sunday)) thisWeekProfit += profit.Profit;
                if (profit.EventTime.Date >= today.Previous(DayOfWeek.Sunday, 2) && profit.EventTime.Date < today.Previous(DayOfWeek.Sunday)) prevWeekProfit += profit.Profit;
                if (profit.EventTime.Date >= today.AddDays(-today.Day + 1)) thisMonthProfit += profit.Profit;
                if (profit.EventTime.Date >= new DateTime(today.Year, 1, 1)) thisYearProfit += profit.Profit;
                all += profit.Profit;

                // assign to the window counters
                if (profit.EventTime >= window1d) d1 += profit.Profit;
                if (profit.EventTime >= window7d) d7 += profit.Profit;
                if (profit.EventTime >= window30d) d30 += profit.Profit;

                // if the commission was taken from the sell asset then remove it from profit
                foreach (var loss in lookup[(profit.Symbol.QuoteAsset, profit.SellOrderId, profit.SellTradeId)].Select(x => x.Commission))
                {
                    // assign to the appropriate counters
                    if (profit.EventTime.Date == today) todayProfit -= loss;
                    if (profit.EventTime.Date == today.AddDays(-1)) yesterdayProfit -= loss;
                    if (profit.EventTime.Date >= today.Previous(DayOfWeek.Sunday)) thisWeekProfit -= loss;
                    if (profit.EventTime.Date >= today.Previous(DayOfWeek.Sunday, 2) && profit.EventTime.Date < today.Previous(DayOfWeek.Sunday)) prevWeekProfit -= loss;
                    if (profit.EventTime.Date >= today.AddDays(-today.Day + 1)) thisMonthProfit -= loss;
                    if (profit.EventTime.Date >= new DateTime(today.Year, 1, 1)) thisYearProfit -= loss;
                    all -= loss;

                    // assign to the window counters
                    if (profit.EventTime >= window1d) d1 -= loss;
                    if (profit.EventTime >= window7d) d7 -= loss;
                    if (profit.EventTime >= window30d) d30 -= loss;
                }
            }

            return new Profit(
                symbol.Name,
                symbol.BaseAsset,
                symbol.QuoteAsset,
                todayProfit,
                yesterdayProfit,
                thisWeekProfit,
                prevWeekProfit,
                thisMonthProfit,
                thisYearProfit,
                all,
                d1,
                d7,
                d30);
        }
    }

    public record Stats(decimal AvgPerHourDay1, decimal AvgPerHourDay7, decimal AvgPerHourDay30, decimal AvgPerDay1, decimal AvgPerDay7, decimal AvgPerDay30)
    {
        public static Stats Zero { get; } = new Stats(0, 0, 0, 0, 0, 0);

        public static Stats FromProfit(Profit profit)
        {
            if (profit is null) throw new ArgumentNullException(nameof(profit));

            return new Stats(
                profit.D1 / 24m,
                profit.D7 / (24m * 7m),
                profit.D30 / (24m * 30m),
                profit.D1,
                profit.D7 / 7m,
                profit.D30 / 30m);
        }
    }

    [Immutable]
    public record ProfitEvent(
        Symbol Symbol,
        DateTime EventTime,
        long BuyOrderId,
        long BuyTradeId,
        long SellOrderId,
        long SellTradeId,
        decimal Quantity,
        decimal BuyPrice,
        decimal SellPrice)
    {
        public decimal BuyValue => Quantity * BuyPrice;
        public decimal SellValue => Quantity * SellPrice;
        public decimal Profit => (SellPrice - BuyPrice) * Quantity;
    }

    [Immutable]
    public record CommissionEvent(
        Symbol Symbol,
        DateTime EventTime,
        long OrderId,
        long TradeId,
        string Asset,
        decimal Commission);
}