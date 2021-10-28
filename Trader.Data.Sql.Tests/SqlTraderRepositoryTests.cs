﻿using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Outcompute.Trader.Models;
using System;
using System.Threading.Tasks;
using System.Transactions;
using Xunit;

namespace Outcompute.Trader.Data.Sql.Tests
{
    public class SqlTraderRepositoryTests
    {
        private const string ConnectionString = @"server=(localdb)\mssqllocaldb;database=TraderTest";

        private static readonly IMapper _mapper = new MapperConfiguration(options =>
        {
            options.AddProfile<SqlTradingRepositoryProfile>();
        }).CreateMapper();

        private static SqlTradingRepository CreateRepository()
        {
            return new SqlTradingRepository(
                Options.Create(new SqlTradingRepositoryOptions { ConnectionString = ConnectionString }),
                NullLogger<SqlTradingRepository>.Instance,
                _mapper);
        }

        private readonly SqlTradingRepository _repository;

        public SqlTraderRepositoryTests()
        {
            _repository = CreateRepository();
        }

        [Fact]
        public async Task SetsAndGetsBalances()
        {
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

            // arrange
            var balance1 = new Balance(Guid.NewGuid().ToString(), 111, 222, DateTime.UtcNow);
            var balance2 = new Balance(Guid.NewGuid().ToString(), 333, 444, DateTime.UtcNow);
            var balances = new[] { balance1, balance2 };

            // act
            await _repository.SetBalancesAsync(balances);
            var result1 = await _repository.TryGetBalanceAsync(balance1.Asset);
            var result2 = await _repository.TryGetBalanceAsync(balance2.Asset);
            var result3 = await _repository.TryGetBalanceAsync(Guid.NewGuid().ToString());

            // assert
            Assert.Equal(balance1, result1);
            Assert.Equal(balance2, result2);
            Assert.Null(result3);
        }

        [Fact]
        public async Task SetsAndGetsOrder()
        {
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

            // arrange
            var order = new OrderQueryResult(Guid.NewGuid().ToString(), 1, 0, string.Empty, 123, 1000, 0, 0, OrderStatus.New, TimeInForce.GoodTillCanceled, OrderType.Limit, OrderSide.Buy, 0, 0, DateTime.UtcNow, DateTime.UtcNow, false, 0);

            // act
            await _repository.SetOrderAsync(order);
            var result = await _repository.GetOrdersAsync(order.Symbol);

            // assert
            Assert.Collection(result, x =>
            {
                Assert.Equal(order.Symbol, x.Symbol);
                Assert.Equal(order.OrderId, x.OrderId);
            });
        }
    }
}