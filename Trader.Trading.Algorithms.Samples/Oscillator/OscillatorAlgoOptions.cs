﻿using System.ComponentModel.DataAnnotations;

namespace Outcompute.Trader.Trading.Algorithms.Samples.Oscillator;

public class OscillatorAlgoOptions
{
    [Required, Range(typeof(TimeSpan), "0.00:00:00.000", "99.00:00:00.000")]
    public TimeSpan BuyCooldown { get; set; } = TimeSpan.FromDays(1);

    [Required, Range(0, double.MaxValue)]
    public decimal EntryNotionalRate { get; set; } = 0.01M;

    public decimal MinNotional { get; set; } = 0;

    [Required]
    public bool UseProfits { get; set; } = false;

    [Required]
    public bool LossEnabled { get; set; } = false;

    [Required, Range(1, 100)]
    public decimal AtrMultiplier { get; set; } = 3;

    [Required]
    public bool TrackingStopEnabled { get; set; } = false;

    [Required]
    public ISet<string> ExcludeFromOpening { get; } = new HashSet<string>();
}