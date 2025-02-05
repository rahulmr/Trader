﻿using Orleans.Concurrency;
using System.Collections.Immutable;

namespace Outcompute.Trader.Models;

public record CancelOrderResult();

[Immutable]
public record CancelStandardOrderResult(
    string Symbol,
    string OriginalClientOrderId,
    long OrderId,
    long OrderListId,
    string ClientOrderId,
    decimal Price,
    decimal OriginalQuantity,
    decimal ExecutedQuantity,
    decimal CummulativeQuoteQuantity,
    OrderStatus Status,
    TimeInForce TimeInForce,
    OrderType Type,
    OrderSide Side) : CancelOrderResult
{
    public static CancelStandardOrderResult Empty { get; } = new CancelStandardOrderResult(
        string.Empty,
        string.Empty,
        0,
        0,
        string.Empty,
        0m,
        0m,
        0m,
        0m,
        OrderStatus.None,
        TimeInForce.None,
        OrderType.None,
        OrderSide.None);
}

public record CancelOcoOrderResult(
    long OrderListId,
    ContingencyType ContingencyType,
    OcoStatus ListStatusType,
    OcoOrderStatus ListOrderStatus,
    string ListClientOrderId,
    DateTime TransactionTime,
    string Symbol,
    ImmutableList<CancelOcoOrderOrderResult> Orders,
    ImmutableList<CancelOcoOrderOrderReportResult> OrderReports) : CancelOrderResult;

public record CancelOcoOrderOrderResult(
    string Symbol,
    long OrderId,
    string ClientOrderId);

public record CancelOcoOrderOrderReportResult(
    string Symbol,
    string OriginalClientOrderId,
    long OrderId,
    long OrderListId,
    string ClientOrderId,
    decimal Price,
    decimal OriginalQuantity,
    decimal ExecutedQuantity,
    decimal CummulativeQuoteQuantity,
    OrderStatus Status,
    TimeInForce TimeInForce,
    OrderType Type,
    OrderSide Side,
    decimal StopPrice,
    decimal IcebergQuantity);