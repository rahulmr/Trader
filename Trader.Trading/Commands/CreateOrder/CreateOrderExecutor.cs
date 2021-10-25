﻿using Microsoft.Extensions.Logging;
using Outcompute.Trader.Trading.Algorithms;
using Outcompute.Trader.Trading.Providers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Outcompute.Trader.Trading.Commands.CreateOrder
{
    internal class CreateOrderExecutor : IAlgoCommandExecutor<CreateOrderCommand>
    {
        private readonly ILogger _logger;
        private readonly ITradingService _trader;
        private readonly IOrderProvider _orders;

        public CreateOrderExecutor(ILogger<CreateOrderExecutor> logger, ITradingService trader, IOrderProvider orders)
        {
            _logger = logger;
            _trader = trader;
            _orders = orders;
        }

        private static string TypeName => nameof(CreateOrderExecutor);

        public async Task ExecuteAsync(IAlgoContext context, CreateOrderCommand command, CancellationToken cancellationToken = default)
        {
            var watch = Stopwatch.StartNew();

            _logger.LogInformation(
                "{Type} {Name} placing {OrderType} {OrderSide} order for {Quantity:F8} {Asset} at {Price:F8} {Quote} for a total of {Total:F8} {Quote}",
                TypeName, command.Symbol.Name, command.Type, command.Side, command.Quantity, command.Symbol.BaseAsset, command.Price, command.Symbol.QuoteAsset, command.Quantity * command.Price, command.Symbol.QuoteAsset);

            var created = await _trader
                .CreateOrderAsync(command.Symbol.Name, command.Side, command.Type, command.TimeInForce, command.Quantity, null, command.Price, command.Tag, null, null, cancellationToken)
                .ConfigureAwait(false);

            await _orders
                .SetOrderAsync(created, 0m, 0m, 0m, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "{Type} {Name} placed {OrderType} {OrderSide} order for {Quantity:F8} {Asset} at {Price:F8} {Quote} for a total of {Total:F8} {Quote} in {ElapsedMs}ms",
                TypeName, command.Symbol.Name, command.Type, command.Side, command.Quantity, command.Symbol.BaseAsset, command.Price, command.Symbol.QuoteAsset, command.Quantity * command.Price, command.Symbol.QuoteAsset, watch.ElapsedMilliseconds);
        }
    }
}