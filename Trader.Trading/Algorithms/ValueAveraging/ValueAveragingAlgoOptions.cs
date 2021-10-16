﻿using Outcompute.Trader.Models;
using System.ComponentModel.DataAnnotations;

namespace Outcompute.Trader.Trading.Algorithms.ValueAveraging
{
    public class ValueAveragingAlgoOptions
    {
        [Required]
        [Range(0.01, 0.99)]
        public decimal PullbackRatio { get; set; } = 0.90m;

        [Required]
        [Range(0.01, 0.99)]
        public decimal BuyOrderSafetyRatio { get; set; } = 0.99m;

        [Required]
        [Range(0.001, 1)]
        public decimal TargetQuoteBalanceFractionPerBuy { get; set; } = 0.001m;

        [Required]
        [Range(1, 2)]
        public decimal ProfitMultipler { get; set; } = 1.1m;

        public decimal? MaxNotional { get; set; }

        [Required]
        public bool IsOpeningEnabled { get; set; } = true;

        [Required]
        public bool IsAveragingEnabled { get; set; } = true;

        [Required]
        public bool RedeemSavings { get; set; } = false;

        [Required]
        public KlineInterval KlineInterval { get; set; } = KlineInterval.Days1;

        [Required]
        public int SmaPeriodsA { get; set; } = 7;

        [Required]
        public int SmaPeriodsB { get; set; } = 25;

        [Required]
        public int SmaPeriodsC { get; set; } = 99;

        [Required]
        public int RsiPeriodsA { get; set; } = 6;

        [Required]
        public int RsiPeriodsB { get; set; } = 12;

        [Required]
        public int RsiPeriodsC { get; set; } = 24;

        [Required]
        public decimal RsiOverboughtA { get; set; } = 30m;

        [Required]
        public decimal RsiOversoldA { get; set; } = 70m;

        [Required]
        public decimal RsiOverboughtB { get; set; } = 30m;

        [Required]
        public decimal RsiOversoldB { get; set; } = 70m;

        [Required]
        public decimal RsiOverboughtC { get; set; } = 30m;

        [Required]
        public decimal RsiOversoldC { get; set; } = 70m;

        public static ValueAveragingAlgoOptions Default { get; } = new ValueAveragingAlgoOptions();
    }
}