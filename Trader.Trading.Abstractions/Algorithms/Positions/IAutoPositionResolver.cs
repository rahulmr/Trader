﻿using Outcompute.Trader.Models;
using Outcompute.Trader.Models.Collections;

namespace Outcompute.Trader.Trading.Algorithms.Positions;

public interface IAutoPositionResolver
{
    AutoPosition Resolve(Symbol symbol, OrderCollection orders, IEnumerable<AccountTrade> trades, DateTime startTime);
}