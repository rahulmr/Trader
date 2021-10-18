﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Concurrency;
using Outcompute.Trader.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using static System.String;

namespace Outcompute.Trader.Trading.Providers
{
    [Reentrant]
    [StatelessWorker(1)]
    internal class OrderProviderReplicaGrain : Grain, IOrderProviderReplicaGrain
    {
        private readonly ReactiveOptions _options;
        private readonly IGrainFactory _factory;
        private readonly IHostApplicationLifetime _lifetime;

        public OrderProviderReplicaGrain(IOptions<ReactiveOptions> options, IGrainFactory factory, IHostApplicationLifetime lifetime)
        {
            _options = options.Value;
            _factory = factory;
            _lifetime = lifetime;
        }

        /// <summary>
        /// The symbol that this grain holds orders for.
        /// </summary>
        private string _symbol = Empty;

        /// <summary>
        /// The serial version of this grain.
        /// Helps detect serial resets from the source grain.
        /// </summary>
        private Guid _version;

        /// <summary>
        /// The last known change serial.
        /// </summary>
        private int _serial;

        /// <summary>
        /// Holds the order cache in a form that is mutable but still convertible to immutable upon request with low overhead.
        /// </summary>
        private readonly ImmutableSortedSet<OrderQueryResult>.Builder _orders = ImmutableSortedSet.CreateBuilder(OrderQueryResult.OrderIdComparer);

        public override async Task OnActivateAsync()
        {
            _symbol = this.GetPrimaryKeyString();

            await LoadAsync();

            RegisterTimer(TickUpdateAsync, null, _options.ReactiveTickDelay, _options.ReactiveTickDelay);

            await base.OnActivateAsync();
        }

        public Task<OrderQueryResult?> TryGetOrderAsync(long orderId)
        {
            var result = _orders.TryGetValue(OrderQueryResult.Empty with { OrderId = orderId }, out var order) ? order : null;

            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<OrderQueryResult>> GetOrdersAsync()
        {
            return Task.FromResult<IReadOnlyList<OrderQueryResult>>(_orders.ToImmutable());
        }

        public async Task SetOrdersAsync(IEnumerable<OrderQueryResult> orders)
        {
            // let the main grain handle saving so it updates every other replica
            await _factory.GetOrderProviderGrain(_symbol).SetOrdersAsync(orders);

            // apply the orders to this replica now so they are consistent from the point of view of the algo calling this method
            // the updated serial numbers will eventually come through as reactive caching calls resolve
            Apply(orders);
        }

        public async Task SetOrderAsync(OrderQueryResult order)
        {
            // let the main grain handle saving so it updates every other replica
            await _factory.GetOrderProviderGrain(_symbol).SetOrderAsync(order);

            // apply the orders to this replica now so they are consistent from the point of view of the algo calling this method
            // the updated serial numbers will eventually come through as reactive caching calls resolve
            Apply(order);
        }

        private async Task LoadAsync()
        {
            // get all the orders
            var result = await _factory.GetOrderProviderGrain(_symbol).GetOrdersAsync();

            Apply(result.Orders);
        }

        private async Task TickUpdateAsync(object _)
        {
            // wait for new orders
            using var gct = new GrainCancellationTokenSource();
            using var reg = _lifetime.ApplicationStopping.Register(() => gct.Cancel());

            var result = await _factory.GetOrderProviderGrain(_symbol).PollOrdersAsync(_version, _serial + 1, gct.Token);

            Apply(result.Version, result.MaxSerial, result.Orders);
        }

        private void Apply(Guid version, int serial, IEnumerable<OrderQueryResult> orders)
        {
            Apply(version, serial);

            foreach (var order in orders)
            {
                Apply(order);
            }
        }

        private void Apply(Guid version, int serial)
        {
            _version = version;
            _serial = serial;
        }

        private void Apply(IEnumerable<OrderQueryResult> orders)
        {
            foreach (var order in orders)
            {
                Apply(order);
            }
        }

        private void Apply(OrderQueryResult order)
        {
            // remove old order to allow an update
            _orders.Remove(order);

            // add new or updated order
            _orders.Add(order);
        }
    }
}