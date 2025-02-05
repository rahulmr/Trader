﻿using Outcompute.Trader.Core.Mathematics;
using Outcompute.Trader.Indicators;

namespace Outcompute.Trader.Trading.Tests.Indicators;

public class AwesomeOscillatorTests
{
    [Fact]
    public void CalculatesAwesomeOscillator()
    {
        // act
        using var indicator = TestData.BtcBusdHistoricalData.Take(10).ToHL().Identity().AwesomeOscillator(3, 5);

        // assert
        Assert.Collection(indicator,
            x => Assert.Null(x),
            x => Assert.Null(x),
            x => Assert.Null(x),
            x => Assert.Null(x),
            x => Assert.Equal(-65.69M, MathN.Round(x, 2)),
            x => Assert.Equal(-631.68M, MathN.Round(x, 2)),
            x => Assert.Equal(-790.94M, MathN.Round(x, 2)),
            x => Assert.Equal(-1010.95M, MathN.Round(x, 2)),
            x => Assert.Equal(125.03M, MathN.Round(x, 2)),
            x => Assert.Equal(353.57M, MathN.Round(x, 2)));
    }
}