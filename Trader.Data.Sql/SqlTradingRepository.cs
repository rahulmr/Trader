﻿using AutoMapper;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Outcompute.Trader.Data.Sql.Models;
using Outcompute.Trader.Models;
using Polly;
using Polly.Retry;
using System.Collections.Concurrent;
using System.Data;

namespace Outcompute.Trader.Data.Sql;

internal partial class SqlTradingRepository : ITradingRepository
{
    private readonly SqlTradingRepositoryOptions _options;
    private readonly ILogger _logger;
    private readonly IMapper _mapper;

    public SqlTradingRepository(IOptions<SqlTradingRepositoryOptions> options, ILogger<SqlTradingRepository> logger, IMapper mapper)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));

        // cache the retry policy to create less garbage
        _retryPolicy = Policy
            .Handle<SqlException>()
            .RetryAsync(_options.RetryCount, (ex, retry) =>
            {
                LogHandledExceptionWillRetry(ex, Name, retry, _options.RetryCount);
            });
    }

    private static string Name => nameof(SqlTradingRepository);

    private readonly AsyncRetryPolicy _retryPolicy;

    private readonly ConcurrentDictionary<string, int> _symbolLookup = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IEnumerable<OrderQueryResult>> GetOrdersAsync(string symbol, CancellationToken cancellationToken = default)
    {
        _ = symbol ?? throw new ArgumentNullException(nameof(symbol));

        using var connection = new SqlConnection(_options.ConnectionString);

        var entities = await connection
            .QueryAsync<OrderEntity>(
                new CommandDefinition(
                    "[dbo].[GetOrders]",
                    new
                    {
                        Symbol = symbol
                    },
                    null,
                    _options.CommandTimeoutAsInteger,
                    CommandType.StoredProcedure,
                    CommandFlags.Buffered,
                cancellationToken))
            .ConfigureAwait(false);

        return _mapper.Map<IEnumerable<OrderQueryResult>>(entities);
    }

    public Task SetOrderAsync(OrderQueryResult order, CancellationToken cancellationToken = default)
    {
        if (order is null) throw new ArgumentNullException(nameof(order));

        return SetOrderCoreAsync(order, cancellationToken);
    }

    private async Task SetOrderCoreAsync(OrderQueryResult order, CancellationToken cancellationToken = default)
    {
        var entity = _mapper.Map<OrderEntity>(order);

        using var connection = new SqlConnection(_options.ConnectionString);

        await connection
            .ExecuteAsync(new CommandDefinition(
                "[dbo].[SetOrder]",
                entity,
                null,
                _options.CommandTimeoutAsInteger,
                CommandType.StoredProcedure,
                CommandFlags.Buffered,
                cancellationToken))
            .ConfigureAwait(false);
    }

    public Task SetOrdersAsync(IEnumerable<OrderQueryResult> orders, CancellationToken cancellationToken = default)
    {
        if (orders is null) throw new ArgumentNullException(nameof(orders));

        return SetOrdersCoreAsync(orders, cancellationToken);
    }

    private async Task SetOrdersCoreAsync(IEnumerable<OrderQueryResult> orders, CancellationToken cancellationToken = default)
    {
        // get the cached ids for the incoming symbols
        var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var symbol in orders.Select(x => x.Symbol))
        {
            // check the local fast dictionary
            if (!ids.ContainsKey(symbol))
            {
                // defer to the slower shared dictionary and database
                ids.Add(symbol, await GetOrAddSymbolAsync(symbol, cancellationToken).ConfigureAwait(false));
            }
        }

        // pass the fast lookup to mapper so it knows how to populate the symbol ids
        var entities = _mapper.Map<IEnumerable<OrderTableParameterEntity>>(orders, options =>
        {
            options.Items[nameof(OrderTableParameterEntity.SymbolId)] = ids;
        });

        using var connection = new SqlConnection(_options.ConnectionString);

        await _retryPolicy
            .ExecuteAsync(ct => connection
                .ExecuteAsync(
                    new CommandDefinition(
                        "[dbo].[SetOrders]",
                        new
                        {
                            Orders = entities.AsSqlDataRecords().AsTableValuedParameter("[dbo].[OrderTableParameter]")
                        },
                        null,
                        _options.CommandTimeoutAsInteger,
                        CommandType.StoredProcedure,
                        CommandFlags.Buffered,
                    ct)),
                    cancellationToken,
                    false)
            .ConfigureAwait(false);
    }

    public Task SetTradeAsync(AccountTrade trade, CancellationToken cancellationToken = default)
    {
        if (trade is null) throw new ArgumentNullException(nameof(trade));

        return SetTradesAsync(Enumerable.Repeat(trade, 1), cancellationToken);
    }

    public async Task SetTradesAsync(IEnumerable<AccountTrade> trades, CancellationToken cancellationToken = default)
    {
        _ = trades ?? throw new ArgumentNullException(nameof(trades));

        // get the cached ids for the incoming symbols
        var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var symbol in trades.Select(x => x.Symbol))
        {
            // check the local fast dictionary
            if (!ids.ContainsKey(symbol))
            {
                // defer to the slower shared dictionary and database
                ids.Add(symbol, await GetOrAddSymbolAsync(symbol, cancellationToken).ConfigureAwait(false));
            }
        }

        // pass the fast lookup to mapper so it knows how to populate the symbol ids
        var entities = _mapper.Map<IEnumerable<TradeTableParameterEntity>>(trades, options =>
        {
            options.Items[nameof(TradeTableParameterEntity.SymbolId)] = ids;
        });

        using var connection = new SqlConnection(_options.ConnectionString);

        await _retryPolicy
            .ExecuteAsync(ct => connection
                .ExecuteAsync(
                    new CommandDefinition(
                        "[dbo].[SetTrades]",
                        new
                        {
                            Trades = entities.AsSqlDataRecords().AsTableValuedParameter("[dbo].[TradeTableParameter]")
                        },
                        null,
                        _options.CommandTimeoutAsInteger,
                        CommandType.StoredProcedure,
                        CommandFlags.Buffered,
                        ct)),
                    cancellationToken,
                    false)
            .ConfigureAwait(false);
    }

    public async Task<IEnumerable<AccountTrade>> GetTradesAsync(string symbol, CancellationToken cancellationToken = default)
    {
        _ = symbol ?? throw new ArgumentNullException(nameof(symbol));

        using var connection = new SqlConnection(_options.ConnectionString);

        var result = await connection
            .QueryAsync<TradeEntity>(
                new CommandDefinition(
                    "[dbo].[GetTrades]",
                    new
                    {
                        Symbol = symbol
                    },
                    null,
                    _options.CommandTimeoutAsInteger,
                    CommandType.StoredProcedure,
                    CommandFlags.Buffered,
                cancellationToken))
            .ConfigureAwait(false);

        return _mapper.Map<IEnumerable<AccountTrade>>(result);
    }

    public async ValueTask SetBalancesAsync(IEnumerable<Balance> balances, CancellationToken cancellationToken = default)
    {
        _ = balances ?? throw new ArgumentNullException(nameof(balances));

        using var connection = new SqlConnection(_options.ConnectionString);

        var entities = _mapper.Map<IEnumerable<BalanceTableParameterEntity>>(balances);

        await _retryPolicy
            .ExecuteAsync(ct => connection
                .ExecuteAsync(
                    new CommandDefinition(
                        "[dbo].[SetBalances]",
                        new
                        {
                            Balances = entities.AsSqlDataRecords().AsTableValuedParameter("[dbo].[BalanceTableParameter]")
                        },
                        null,
                        _options.CommandTimeoutAsInteger,
                        CommandType.StoredProcedure,
                        CommandFlags.Buffered,
                    ct)),
                    cancellationToken,
                    false)
            .ConfigureAwait(false);
    }

    public async ValueTask<IEnumerable<Balance>> GetBalancesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(_options.ConnectionString);

        var entities = await connection
            .QueryAsync<BalanceEntity>(new CommandDefinition("[dbo].[GetBalances]", null, null, _options.CommandTimeoutAsInteger, CommandType.StoredProcedure, CommandFlags.Buffered, cancellationToken))
            .ConfigureAwait(false);

        return _mapper.Map<IEnumerable<Balance>>(entities);
    }

    public async ValueTask<Balance?> TryGetBalanceAsync(string asset, CancellationToken cancellationToken = default)
    {
        _ = asset ?? throw new ArgumentNullException(nameof(asset));

        using var connection = new SqlConnection(_options.ConnectionString);

        var entity = await connection
            .QuerySingleOrDefaultAsync<BalanceEntity>(
                new CommandDefinition(
                    "[dbo].[GetBalance]",
                    new
                    {
                        Asset = asset
                    },
                    null,
                    _options.CommandTimeoutAsInteger,
                    CommandType.StoredProcedure,
                    CommandFlags.Buffered,
                    cancellationToken))
            .ConfigureAwait(false);

        return _mapper.Map<Balance>(entity);
    }

    private async ValueTask<int> GetOrAddSymbolAsync(string symbol, CancellationToken cancellation)
    {
        _ = symbol ?? throw new ArgumentNullException(nameof(symbol));

        if (_symbolLookup.TryGetValue(symbol, out var id))
        {
            return id;
        }

        using var connection = new SqlConnection(_options.ConnectionString);

        var parameters = new DynamicParameters();
        parameters.Add("Name", symbol, DbType.String, ParameterDirection.Input);
        parameters.Add("Id", null, DbType.Int32, ParameterDirection.Output);

        await connection
            .ExecuteScalarAsync<int>(
                new CommandDefinition(
                    "[dbo].[GetOrAddSymbol]",
                    parameters,
                    null,
                    _options.CommandTimeoutAsInteger,
                    CommandType.StoredProcedure,
                    CommandFlags.Buffered,
                    cancellation))
            .ConfigureAwait(false);

        id = parameters.Get<int>("Id");

        _symbolLookup.TryAdd(symbol, id);

        return id;
    }

    public Task<MiniTicker?> TryGetTickerAsync(string symbol, CancellationToken cancellationToken = default)
    {
        if (symbol is null) throw new ArgumentNullException(nameof(symbol));

        return TryGetTickerCoreAsync(symbol, cancellationToken);

        async Task<MiniTicker?> TryGetTickerCoreAsync(string symbol, CancellationToken cancellationToken = default)
        {
            using var connection = new SqlConnection(_options.ConnectionString);

            var entity = await connection
                .QuerySingleOrDefaultAsync<TickerEntity>(
                    new CommandDefinition(
                        "[dbo].[GetTicker]",
                        new
                        {
                            Symbol = symbol
                        },
                        null,
                        _options.CommandTimeoutAsInteger,
                        CommandType.StoredProcedure,
                        CommandFlags.Buffered,
                        cancellationToken))
                .ConfigureAwait(false);

            return _mapper.Map<MiniTicker>(entity);
        }
    }

    public async ValueTask SetKlineAsync(Kline item, CancellationToken cancellationToken = default)
    {
        var symbolId = await GetOrAddSymbolAsync(item.Symbol, cancellationToken).ConfigureAwait(false);

        var entity = _mapper.Map<KlineEntity>(item, options =>
        {
            options.Items[nameof(KlineEntity.SymbolId)] = symbolId;
        });

        using var connection = new SqlConnection(_options.ConnectionString);

        await _retryPolicy
            .ExecuteAsync(ct => connection
                .ExecuteAsync(
                    new CommandDefinition(
                        "[dbo].[SetKline]",
                        entity,
                        null,
                        _options.CommandTimeoutAsInteger,
                        CommandType.StoredProcedure,
                        CommandFlags.Buffered,
                    ct)),
                    cancellationToken,
                    false)
            .ConfigureAwait(false);
    }

    public ValueTask SetKlinesAsync(IEnumerable<Kline> items, CancellationToken cancellationToken = default)
    {
        if (items is null) throw new ArgumentNullException(nameof(items));

        return SetKlinesCoreAsync(items, cancellationToken);
    }

    private async ValueTask SetKlinesCoreAsync(IEnumerable<Kline> items, CancellationToken cancellationToken = default)
    {
        // get the cached ids for the incoming symbols
        var symbolIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var symbol in items.Select(x => x.Symbol))
        {
            // check the local fast dictionary
            if (!symbolIds.ContainsKey(symbol))
            {
                // defer to the slower shared dictionary and database
                symbolIds.Add(symbol, await GetOrAddSymbolAsync(symbol, cancellationToken).ConfigureAwait(false));
            }
        }

        var entities = _mapper.Map<IEnumerable<KlineTableParameterEntity>>(items, options =>
        {
            options.Items[nameof(KlineTableParameterEntity.SymbolId)] = symbolIds;
        });

        using var connection = new SqlConnection(_options.ConnectionString);

        await _retryPolicy
            .ExecuteAsync(ct => connection
                .ExecuteAsync(
                    new CommandDefinition(
                        "[dbo].[SetKlines]",
                        new
                        {
                            Klines = entities.AsSqlDataRecords().AsTableValuedParameter("[dbo].[KlineTableParameter]")
                        },
                        null,
                        _options.CommandTimeoutAsInteger,
                        CommandType.StoredProcedure,
                        CommandFlags.Buffered,
                    ct)),
                    cancellationToken,
                    false)
            .ConfigureAwait(false);
    }

    public async ValueTask<IEnumerable<Kline>> GetKlinesAsync(string symbol, KlineInterval interval, DateTime startOpenTime, DateTime endOpenTime, CancellationToken cancellationToken = default)
    {
        _ = symbol ?? throw new ArgumentNullException(nameof(symbol));

        using var connection = new SqlConnection(_options.ConnectionString);

        return await connection
            .QueryAsync<Kline>(
                new CommandDefinition(
                    "[dbo].[GetKlines]",
                    new
                    {
                        Symbol = symbol,
                        Interval = interval,
                        StartOpenTime = startOpenTime,
                        EndOpenTime = endOpenTime
                    },
                    null,
                    _options.CommandTimeoutAsInteger,
                    CommandType.StoredProcedure,
                    CommandFlags.Buffered,
                    cancellationToken))
            .ConfigureAwait(false);
    }

    public Task SetTickerAsync(MiniTicker ticker, CancellationToken cancellationToken = default)
    {
        if (ticker is null) throw new ArgumentNullException(nameof(ticker));

        return SetTickerCoreAsync(ticker, cancellationToken);

        async Task SetTickerCoreAsync(MiniTicker ticker, CancellationToken cancellationToken = default)
        {
            var entity = _mapper.Map<TickerEntity>(ticker);

            using var connection = new SqlConnection(_options.ConnectionString);

            await _retryPolicy
                .ExecuteAsync(ct => connection
                    .ExecuteAsync(
                        new CommandDefinition(
                            "[dbo].[SetTicker]",
                            entity,
                            null,
                            _options.CommandTimeoutAsInteger,
                            CommandType.StoredProcedure,
                            CommandFlags.Buffered,
                        ct)),
                        cancellationToken,
                        false)
                .ConfigureAwait(false);
        }
    }

    [LoggerMessage(0, LogLevel.Error, "{Name} handled exception and will retry ({Retry}/{Total})")]
    private partial void LogHandledExceptionWillRetry(Exception ex, string name, int retry, int total);
}