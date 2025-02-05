﻿using System.Collections.Immutable;

namespace Outcompute.Trader.Models;

public record UserDataStreamMessage()
{
    public static UserDataStreamMessage Empty { get; } = new UserDataStreamMessage();
}

public record OutboundAccountPositionUserDataStreamMessage(
    DateTime EventTime,
    DateTime LastAccountUpdateTime,
    ImmutableList<OutboundAccountPositionBalanceUserDataStreamMessage> Balances)
    : UserDataStreamMessage
{
    public static new OutboundAccountPositionUserDataStreamMessage Empty { get; } =
        new OutboundAccountPositionUserDataStreamMessage(
            DateTime.MinValue,
            DateTime.MinValue,
            ImmutableList<OutboundAccountPositionBalanceUserDataStreamMessage>.Empty);
}

public record OutboundAccountPositionBalanceUserDataStreamMessage(
    string Asset,
    decimal Free,
    decimal Locked)
{
    public static OutboundAccountPositionBalanceUserDataStreamMessage Empty { get; } =
        new OutboundAccountPositionBalanceUserDataStreamMessage(string.Empty, 0, 0);
}

public record BalanceUpdateUserDataStreamMessage(
    DateTime EventTime,
    string Asset,
    decimal BalanceDelta,
    DateTime ClearTime)
    : UserDataStreamMessage;

public record ExecutionReportUserDataStreamMessage(
    DateTime EventTime,
    string Symbol,
    string ClientOrderId,
    OrderSide OrderSide,
    OrderType OrderType,
    TimeInForce TimeInForce,
    decimal OrderQuantity,
    decimal OrderPrice,
    decimal StopPrice,
    decimal IcebergQuantity,
    long OrderListId,
    string OriginalClientOrderId,
    ExecutionType ExecutionType,
    OrderStatus OrderStatus,
    string OrderRejectReason,
    long OrderId,
    decimal LastExecutedQuantity,
    decimal CummulativeFilledQuantity,
    decimal LastExecutedPrice,
    decimal CommissionAmount,
    string CommissionAsset,
    DateTime TransactionTime,
    long TradeId,
    bool IsBookOrder,
    bool IsMakerOrder,
    DateTime OrderCreatedTime,
    decimal CummulativeQuoteAssetTransactedQuantity,
    decimal LastQuoteAssetTransactedQuantity,
    decimal QuoteOrderQuantity)
    : UserDataStreamMessage
{
    public static new ExecutionReportUserDataStreamMessage Empty { get; } = new ExecutionReportUserDataStreamMessage(
        DateTime.MinValue,
        string.Empty,
        string.Empty,
        OrderSide.None,
        OrderType.None,
        TimeInForce.None,
        0,
        0,
        0,
        0,
        0,
        string.Empty,
        ExecutionType.None,
        OrderStatus.None,
        string.Empty,
        0,
        0,
        0,
        0,
        0,
        string.Empty,
        DateTime.MinValue,
        0,
        false,
        false,
        DateTime.MinValue,
        0,
        0,
        0);
}

public record ListStatusUserDataStreamMessage(
    DateTime EventTime,
    string Symbol,
    long OrderListId,
    ContingencyType ContingencyType,
    OcoStatus ListStatusType,
    OcoOrderStatus ListOrderStatus,
    string ListRejectReason,
    string ListClientOrderId,
    DateTime TransactionTime,
    ImmutableList<ListStatusItemUserDataStreamMessage> Items)
    : UserDataStreamMessage;

public record ListStatusItemUserDataStreamMessage(
    string Symbol,
    long OrderId,
    string ClientOrderId);