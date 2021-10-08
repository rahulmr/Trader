﻿using Orleans;
using System.Threading.Tasks;

namespace Outcompute.Trader.Trading.Binance.Providers.UserData
{
    internal interface IBinanceUserDataGrain : IGrainWithGuidKey
    {
        /// <summary>
        /// Returns <see cref="true"/> if the stream is synchronized, otherwise <see cref="false"/>.
        /// </summary>
        Task<bool> IsReadyAsync();
    }
}