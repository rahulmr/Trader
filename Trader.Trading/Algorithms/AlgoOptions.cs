﻿using System.ComponentModel.DataAnnotations;

namespace Outcompute.Trader.Trading.Algorithms;

public class AlgoOptions
{
    /// <summary>
    /// The type name of the algorithm to use.
    /// </summary>
    [Required]
    public string Type { get; set; } = Empty;

    /// <summary>
    /// The default symbol for this algo.
    /// </summary>
    public string Symbol { get; set; } = Empty;

    /// <summary>
    /// A set of symbols for use by this algo.
    /// </summary>
    public ISet<string> Symbols { get; } = new HashSet<string>();

    /// <summary>
    /// The default kline interval for this algo.
    /// </summary>
    public KlineInterval KlineInterval { get; set; } = KlineInterval.None;

    /// <summary>
    /// The default kline periods for this algo.
    /// </summary>
    public int KlinePeriods { get; set; }

    [Required]
    public bool Enabled { get; set; } = true;

    [Required]
    [Range(typeof(TimeSpan), "0.00:00:00.001", "1.00:00:00.000")]
    public TimeSpan MaxExecutionTime { get; set; } = TimeSpan.FromMinutes(1);

    [Required]
    [Range(typeof(TimeSpan), "0.00:00:00.001", "1.00:00:00.000")]
    public TimeSpan TickDelay { get; set; } = TimeSpan.FromSeconds(10);

    [Required]
    [Range(typeof(TimeSpan), "0.00:00:00.001", "1.00:00:00.000")]
    public TimeSpan StopTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Run the algo on its own tick schedule as defined by <see cref="TickDelay"/>.
    /// </summary>
    [Required]
    public bool TickEnabled { get; set; } = false;

    /// <summary>
    /// Run the algo as part of the global batch schedule in an ordered fashion with other algos.
    /// </summary>
    [Required]
    public bool BatchEnabled { get; set; } = true;

    /// <summary>
    /// The relative run order in the batch.
    /// </summary>
    [Required]
    public int BatchOrder { get; set; } = 0;

    /// <summary>
    /// The start time for automatic position calculation.
    /// </summary>
    [Required]
    public DateTime StartTime { get; set; } = DateTime.MinValue;

    public AlgoOptionsDependsOn DependsOn { get; } = new();
}

public class AlgoOptionsDependsOn
{
    public IList<AlgoOptionsDependsOnKlines> Klines { get; } = new List<AlgoOptionsDependsOnKlines>();
}

public class AlgoOptionsDependsOnKlines
{
    public string Symbol { get; set; } = Empty;

    [Required]
    public KlineInterval Interval { get; set; } = KlineInterval.None;

    [Required]
    public int Periods { get; set; }
}