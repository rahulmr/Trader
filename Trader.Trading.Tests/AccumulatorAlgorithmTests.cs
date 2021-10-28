﻿using Microsoft.Extensions.Options;
using Moq;
using Outcompute.Trader.Trading.Algorithms.Accumulator;
using Xunit;

namespace Outcompute.Trader.Trading.Tests
{
    public class AccumulatorAlgorithmTests
    {
        [Fact]
        public void Constructs()
        {
            // arrange
            var name = "SomeSymbol";
            var options = Mock.Of<IOptionsMonitor<AccumulatorAlgoOptions>>(x => x.Get(name) == new AccumulatorAlgoOptions
            {
                Symbol = "SomeSymbol"
            });

            // act
            var algo = new AccumulatorAlgo(options);

            // assert
            Assert.NotNull(algo);
        }
    }
}