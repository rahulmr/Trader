﻿using Outcompute.Trader.Trading.Algorithms.Context;
using Outcompute.Trader.Trading.Commands.CancelOpenOrders;
using Outcompute.Trader.Trading.Providers;

namespace Outcompute.Trader.Trading.Tests;

public class CancelOpenOrdersExecutorTests
{
    [Fact]
    public async Task Executes()
    {
        // arrange
        var symbol = Symbol.Empty with { Name = "ABCXYZ" };
        var orderId = 123;
        var side = OrderSide.Sell;
        var cancelled = CancelStandardOrderResult.Empty with { Symbol = symbol.Name, OrderId = orderId, Status = OrderStatus.Canceled, Side = side };

        var trader = Mock.Of<ITradingService>();
        Mock.Get(trader)
            .Setup(x => x.CancelOrderAsync(symbol.Name, orderId, CancellationToken.None))
            .Returns(Task.FromResult(cancelled))
            .Verifiable();

        var order = OrderQueryResult.Empty with { Symbol = symbol.Name, OrderId = orderId, Status = OrderStatus.New, Side = side };

        var orders = Mock.Of<IOrderProvider>();
        Mock.Get(orders)
            .Setup(x => x.SetOrderAsync(cancelled, CancellationToken.None))
            .Verifiable();

        var context = new AlgoContext(symbol.Name, NullServiceProvider.Instance);
        context.Data.GetOrAdd(symbol.Name).Orders.Open = ImmutableSortedSet.Create(OrderQueryResult.KeyComparer, order);

        var executor = new CancelOpenOrdersExecutor(NullLogger<CancelOpenOrdersExecutor>.Instance, trader, orders);
        var command = new CancelOpenOrdersCommand(symbol, side);

        // act
        await executor.ExecuteAsync(context, command);

        // assert
        Mock.Get(orders).VerifyAll();
        Mock.Get(trader).VerifyAll();
    }
}