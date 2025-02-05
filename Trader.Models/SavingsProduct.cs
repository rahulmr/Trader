﻿namespace Outcompute.Trader.Models;

public record SavingsProduct(
    string Asset,
    decimal AvgAnnualInterestRate,
    bool CanPurchase,
    bool CanRedeem,
    decimal DailyInterestPerThousand,
    bool Featured,
    decimal MinPurchaseAmount,
    string ProductId,
    decimal PurchasedAmount,
    string Status,
    decimal UpLimit,
    decimal UpLimitPerUser);