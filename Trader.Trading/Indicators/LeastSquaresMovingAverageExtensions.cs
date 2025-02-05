﻿using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;

namespace System.Collections.Generic;

public record struct LsmaValue
{
    public bool IsReady { get; init; }
    public decimal Intercept { get; init; }
    public decimal Slope { get; init; }
    public decimal Value { get; init; }
}

public static class LeastSquaresMovingAverageExtensions
{
    public static IEnumerable<LsmaValue> LeastSquaresMovingAverage(this IEnumerable<Kline> source, int periods)
    {
        Guard.IsNotNull(source, nameof(source));
        Guard.IsGreaterThan(periods, 0, nameof(periods));

        var queue = new Queue<double>(periods);
        var time = Vector<double>.Build.Dense(periods, i => i + 1).ToArray();

        foreach (var item in source)
        {
            if (queue.Count >= periods)
            {
                queue.Dequeue();
            }
            queue.Enqueue((double)item.ClosePrice);

            if (queue.Count < periods)
            {
                yield return new LsmaValue
                {
                    IsReady = false,
                    Intercept = 0,
                    Slope = 0,
                    Value = item.ClosePrice
                };
            }
            else
            {
                var result = Fit.Line(time, queue.ToArray());
                var intercept = (decimal)result.Item1;
                var slope = (decimal)result.Item2;
                var value = intercept + slope * periods;

                yield return new LsmaValue
                {
                    IsReady = true,
                    Intercept = intercept,
                    Slope = slope,
                    Value = value
                };
            }
        }
    }
}