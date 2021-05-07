﻿using Trader.Models;
using Trader.Models.Collections;
using static System.String;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ModelsServiceCollectionExtensions
    {
        /// <summary>
        /// Adds shared business-to-business model type converter services for the specialized collections.
        /// </summary>
        public static IServiceCollection AddModelServices(this IServiceCollection services)
        {
            return services
                .AddSingleton(typeof(ImmutableSortedOrderSetConverter<>))
                .AddSingleton(typeof(ImmutableSortedTradeSetConverter<>))
                .AddAutoMapper(options =>
                {
                    options.AddProfile<ImmutableSortedOrderSetProfile>();
                    options.AddProfile<ImmutableSortedTradeSetProfile>();

                    options.CreateMap<Ticker, MiniTicker>()
                        .ForCtorParam(nameof(MiniTicker.EventTime), x => x.MapFrom(y => y.CloseTime))
                        .ForCtorParam(nameof(MiniTicker.ClosePrice), x => x.MapFrom(y => y.LastPrice))
                        .ForCtorParam(nameof(MiniTicker.AssetVolume), x => x.MapFrom(y => y.Volume));

                    options.CreateMap<ExecutionReportUserDataStreamMessage, AccountTrade>()
                        .ForCtorParam(nameof(AccountTrade.Id), x => x.MapFrom(y => y.TradeId))
                        .ForCtorParam(nameof(AccountTrade.Price), x => x.MapFrom(y => y.LastExecutedPrice))
                        .ForCtorParam(nameof(AccountTrade.Quantity), x => x.MapFrom(y => y.LastExecutedQuantity))
                        .ForCtorParam(nameof(AccountTrade.QuoteQuantity), x => x.MapFrom(y => y.LastQuoteAssetTransactedQuantity))
                        .ForCtorParam(nameof(AccountTrade.Commission), x => x.MapFrom(y => y.CommissionAmount))
                        .ForCtorParam(nameof(AccountTrade.Time), x => x.MapFrom(y => y.TransactionTime))
                        .ForCtorParam(nameof(AccountTrade.IsBuyer), x => x.MapFrom(y => y.OrderSide == OrderSide.Buy))
                        .ForCtorParam(nameof(AccountTrade.IsMaker), x => x.MapFrom(y => y.IsMakerOrder))
                        .ForCtorParam(nameof(AccountTrade.IsBestMatch), x => x.MapFrom(_ => true));

                    options.CreateMap<ExecutionReportUserDataStreamMessage, OrderQueryResult>()
                        .ForCtorParam(nameof(OrderQueryResult.ClientOrderId), x => x.MapFrom(y => IsNullOrWhiteSpace(y.OriginalClientOrderId) ? y.ClientOrderId : y.OriginalClientOrderId))
                        .ForCtorParam(nameof(OrderQueryResult.Price), x => x.MapFrom(y => y.OrderPrice))
                        .ForCtorParam(nameof(OrderQueryResult.OriginalQuantity), x => x.MapFrom(y => y.OrderQuantity))
                        .ForCtorParam(nameof(OrderQueryResult.ExecutedQuantity), x => x.MapFrom(y => y.CummulativeFilledQuantity))
                        .ForCtorParam(nameof(OrderQueryResult.CummulativeQuoteQuantity), x => x.MapFrom(y => y.CummulativeQuoteAssetTransactedQuantity))
                        .ForCtorParam(nameof(OrderQueryResult.Status), x => x.MapFrom(y => y.OrderStatus))
                        .ForCtorParam(nameof(OrderQueryResult.Type), x => x.MapFrom(y => y.OrderType))
                        .ForCtorParam(nameof(OrderQueryResult.Side), x => x.MapFrom(y => y.OrderSide))
                        .ForCtorParam(nameof(OrderQueryResult.Time), x => x.MapFrom(y => y.OrderCreatedTime))
                        .ForCtorParam(nameof(OrderQueryResult.UpdateTime), x => x.MapFrom(y => y.TransactionTime))
                        .ForCtorParam(nameof(OrderQueryResult.IsWorking), x => x.MapFrom(_ => true))
                        .ForCtorParam(nameof(OrderQueryResult.OriginalQuoteOrderQuantity), x => x.MapFrom(y => y.QuoteOrderQuantity));
                });
        }
    }
}