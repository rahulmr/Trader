﻿namespace Outcompute.Trader.Trading.Binance.Converters;

internal class PermissionConverter : ITypeConverter<string, Permission>
{
    public Permission Convert(string source, Permission destination, ResolutionContext context)
    {
        return source switch
        {
            null => Permission.None,

            "SPOT" => Permission.Spot,
            "MARGIN" => Permission.Margin,
            "LEVERAGED" => Permission.Leveraged,
            "TRD_GRP_003" => Permission.TrdGrp003,

            _ => throw new AutoMapperMappingException($"Unknown {nameof(Permission)} '{source}'")
        };
    }
}