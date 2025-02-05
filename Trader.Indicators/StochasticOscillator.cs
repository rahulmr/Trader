﻿namespace Outcompute.Trader.Indicators;

public record struct StochasticOscillatorResult(decimal? K, decimal? D)
{
    public static StochasticOscillatorResult Empty { get; } = new();
}

public class StochasticOscillator : CompositeIndicator<HLC, StochasticOscillatorResult>
{
    public const int DefaultPeriodsK = 14;
    public const int DefaultSmoothK = 1;
    public const int DefaultPeriodsD = 3;

    public StochasticOscillator(IndicatorResult<HLC> source, int periodsK = DefaultPeriodsK, int smoothK = DefaultSmoothK, int periodsD = DefaultPeriodsD)
        : base(source, x =>
        {
            Guard.IsNotNull(source, nameof(source));
            Guard.IsGreaterThanOrEqualTo(periodsK, 1, nameof(periodsK));
            Guard.IsGreaterThanOrEqualTo(smoothK, 1, nameof(smoothK));
            Guard.IsGreaterThanOrEqualTo(periodsD, 1, nameof(periodsD));

            var k = Indicator.Sma(Indicator.Stochastic(source, periodsK), smoothK);
            var d = Indicator.Sma(k, periodsD);

            return Indicator.Zip(k, d, (x, y) => new StochasticOscillatorResult(x, y));
        })
    {
    }
}

public static partial class Indicator
{
    public static StochasticOscillator StochasticOscillator(this IndicatorResult<HLC> source, int periodsK = Indicators.StochasticOscillator.DefaultPeriodsK, int smoothK = Indicators.StochasticOscillator.DefaultSmoothK, int periodsD = Indicators.StochasticOscillator.DefaultPeriodsD)
        => new(source, periodsK, smoothK, periodsD);
}