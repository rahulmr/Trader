﻿using Outcompute.Trader.Trading.Algorithms.Context;
using Outcompute.Trader.Trading.Commands.CancelOrder;
using Outcompute.Trader.Trading.Providers;

namespace Outcompute.Trader.Trading.Tests
{
    public class CancelOrderExecutorTests
    {
        [Fact]
        public async Task ExecutesScenario()
        {
            // arrange
            var logger = NullLogger<CancelOrderExecutor>.Instance;
            var symbol = Symbol.Empty with { Name = "ABCXYZ" };
            var orderId = 123;
            var cancelled = CancelStandardOrderResult.Empty with { Symbol = symbol.Name, OrderId = orderId };
            var trader = Mock.Of<ITradingService>();
            Mock.Get(trader)
                .Setup(x => x.CancelOrderAsync(symbol.Name, orderId, CancellationToken.None))
                .Returns(Task.FromResult(cancelled))
                .Verifiable();
            var orders = Mock.Of<IOrderProvider>();
            Mock.Get(orders)
                .Setup(x => x.SetOrderAsync(cancelled, CancellationToken.None))
                .Verifiable();
            var executor = new CancelOrderExecutor(logger, trader, orders);
            var provider = NullServiceProvider.Instance;

            var context = new AlgoContext("Algo1", provider);
            context.Data.GetOrAdd(symbol.Name);

            var command = new CancelOrderCommand(symbol, orderId);

            // act
            await executor.ExecuteAsync(context, command);

            // assert
            Mock.Get(trader).VerifyAll();
            Mock.Get(orders).VerifyAll();
        }
    }
}