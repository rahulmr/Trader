﻿using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Trader.Data.Memory
{
    internal class MemoryTraderRepository : ITraderRepository, IHostedService
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<long, OrderQueryResult>> _orders = new();
        private readonly ConcurrentDictionary<string, long> _maxOrderIds = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<long, OrderQueryResult>> _transientOrders = new();

        private readonly ConcurrentDictionary<string, ConcurrentDictionary<long, AccountTrade>> _trades = new();
        private readonly ConcurrentDictionary<string, long> _maxTradeIds = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<long, ConcurrentDictionary<long, AccountTrade>>> _tradesByOrder = new();

        #region Trader Repository

        public Task<long> GetMaxOrderIdAsync(string symbol, CancellationToken cancellationToken = default)
        {
            if (_maxOrderIds.TryGetValue(symbol, out var value))
            {
                return Task.FromResult(value);
            }

            return Task.FromResult(0L);
        }

        public Task<long> GetMaxTradeIdAsync(string symbol, CancellationToken cancellationToken = default)
        {
            if (_maxTradeIds.TryGetValue(symbol, out var value))
            {
                return Task.FromResult(value);
            }

            return Task.FromResult(0L);
        }

        public Task<long> GetMinTransientOrderIdAsync(string symbol, CancellationToken cancellationToken = default)
        {
            var result = long.MaxValue;

            if (_transientOrders.TryGetValue(symbol, out var lookup))
            {
                foreach (var item in lookup)
                {
                    if (item.Key < result)
                    {
                        result = item.Key;
                    }
                }
            }

            return Task.FromResult(result is long.MaxValue ? 0 : result);
        }

        public Task<SortedOrderSet> GetOrdersAsync(string symbol, CancellationToken cancellationToken = default)
        {
            var result = new SortedOrderSet();

            if (_orders.TryGetValue(symbol, out var lookup))
            {
                foreach (var item in lookup)
                {
                    result.Add(item.Value);
                }
            }

            return Task.FromResult(result);
        }

        public Task<SortedTradeSet> GetTradesAsync(string symbol, long? orderId = null, CancellationToken cancellationToken = default)
        {
            var result = new SortedTradeSet();

            if (orderId.HasValue)
            {
                if (_tradesByOrder.TryGetValue(symbol, out var lookup1) && lookup1.TryGetValue(orderId.Value, out var lookup2))
                {
                    foreach (var item in lookup2)
                    {
                        result.Add(item.Value);
                    }
                }
            }
            else
            {
                if (_trades.TryGetValue(symbol, out var lookup))
                {
                    foreach (var item in lookup)
                    {
                        result.Add(item.Value);
                    }
                }
            }

            return Task.FromResult(result);
        }

        public Task<SortedOrderSet> GetTransientOrdersAsync(string symbol, OrderSide? orderSide = null, bool? significant = null, CancellationToken cancellationToken = default)
        {
            var result = new SortedOrderSet();

            if (_transientOrders.TryGetValue(symbol, out var lookup))
            {
                var query = lookup.AsEnumerable();

                if (orderSide.HasValue)
                {
                    query = query.Where(x => x.Value.Side == orderSide.Value);
                }

                if (significant.HasValue)
                {
                    if (significant.Value)
                    {
                        query = query.Where(x => x.Value.ExecutedQuantity > 0m);
                    }
                    else
                    {
                        query = query.Where(x => x.Value.ExecutedQuantity <= 0m);
                    }
                }

                foreach (var item in query)
                {
                    result.Add(item.Value);
                }
            }

            return Task.FromResult(result);
        }

        public Task SetOrdersAsync(IEnumerable<OrderQueryResult> orders, CancellationToken cancellationToken = default)
        {
            if (orders is null) throw new ArgumentNullException(nameof(orders));

            foreach (var order in orders)
            {
                _orders
                    .GetOrAdd(order.Symbol, _ => new ConcurrentDictionary<long, OrderQueryResult>())
                    .AddOrUpdate(order.OrderId, order, (key, current) => order);

                // update the max order id index
                _maxOrderIds.AddOrUpdate(order.Symbol, order.OrderId, (key, current) => order.OrderId > current ? order.OrderId : current);

                // update the transient order index
                if (order.Status.IsTransientStatus())
                {
                    _transientOrders
                        .GetOrAdd(order.Symbol, _ => new ConcurrentDictionary<long, OrderQueryResult>())
                        .AddOrUpdate(order.OrderId, order, (key, current) => order);
                }
                else
                {
                    _transientOrders
                        .GetOrAdd(order.Symbol, _ => new ConcurrentDictionary<long, OrderQueryResult>())
                        .TryRemove(order.OrderId, out _);
                }
            }

            return Task.CompletedTask;
        }

        public Task SetTradesAsync(IEnumerable<AccountTrade> trades, CancellationToken cancellationToken = default)
        {
            if (trades is null) throw new ArgumentNullException(nameof(trades));

            foreach (var trade in trades)
            {
                _trades
                    .GetOrAdd(trade.Symbol, _ => new ConcurrentDictionary<long, AccountTrade>())
                    .AddOrUpdate(trade.Id, trade, (k, e) => trade);

                // update the max trade id index
                _maxTradeIds.AddOrUpdate(trade.Symbol, trade.Id, (key, current) => trade.Id > current ? trade.Id : current);

                // update the trades by order index
                _tradesByOrder
                    .GetOrAdd(trade.Symbol, _ => new ConcurrentDictionary<long, ConcurrentDictionary<long, AccountTrade>>())
                    .GetOrAdd(trade.OrderId, _ => new ConcurrentDictionary<long, AccountTrade>())
                    .AddOrUpdate(trade.Id, trade, (key, current) => trade);
            }

            return Task.CompletedTask;
        }

        #endregion Trader Repository

        #region Hosted Service

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        #endregion Hosted Service
    }
}