﻿using Microsoft.Extensions.DependencyInjection;
using Moq;
using Outcompute.Trader.Models;
using Outcompute.Trader.Trading.Algorithms.Context;
using Outcompute.Trader.Trading.Commands;
using Outcompute.Trader.Trading.Commands.AveragingSell;
using Xunit;

namespace Outcompute.Trader.Trading.Tests
{
    public class AveragingSellCommandTests
    {
        [Fact]
        public async Task Executes()
        {
            // arrange
            var symbol = Symbol.Empty;
            var profitMultiplier = 1.10m;
            var redeemSavings = true;
            var redeemSwapPool = true;
            var executor = Mock.Of<IAlgoCommandExecutor<AveragingSellCommand>>();
            var provider = new ServiceCollection()
                .AddSingleton(executor)
                .BuildServiceProvider();
            var context = new AlgoContext("Algo1", provider);
            var command = new AveragingSellCommand(symbol, profitMultiplier, redeemSavings, redeemSwapPool);

            // act
            await command.ExecuteAsync(context);

            // assert
            Assert.Equal(symbol, command.Symbol);
            Assert.Equal(profitMultiplier, command.ProfitMultiplier);
            Assert.Equal(redeemSavings, command.RedeemSavings);
            Mock.Get(executor).Verify(x => x.ExecuteAsync(context, command, CancellationToken.None));
        }
    }
}