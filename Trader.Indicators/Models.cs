﻿namespace Outcompute.Trader.Indicators;

public record struct OHLCV(decimal? Open, decimal? High, decimal? Low, decimal? Close, decimal? Volume)
{
    public static OHLCV Empty { get; } = new();

    public static implicit operator HLC(OHLCV value) => new(value.High, value.Low, value.Close);

    public static implicit operator HL(OHLCV value) => new(value.High, value.Low);

    public static implicit operator CV(OHLCV value) => new(value.Close, value.Volume);
}

public record struct HLC(decimal? High, decimal? Low, decimal? Close)
{
    public static implicit operator HL(HLC value) => new(value.High, value.Low);
}

public record struct HL(decimal? High, decimal? Low);

public record struct CV(decimal? Close, decimal? Volume);

public static class ModelConversionExtensions
{
    public static HL ToHL(this OHLCV item) => new(item.High, item.Low);

    public static CV ToCV(this OHLCV item) => new(item.Close, item.Volume);

    public static HLC ToHLC(this OHLCV item) => new(item.High, item.Low, item.Close);

    public static IEnumerable<HL> ToHL(this IEnumerable<OHLCV> source) => source.Select(x => x.ToHL());

    public static IEnumerable<CV> ToCV(this IEnumerable<OHLCV> source) => source.Select(x => x.ToCV());

    public static IEnumerable<HLC> ToHLC(this IEnumerable<OHLCV> source) => source.Select(x => x.ToHLC());

    public static IEnumerable<decimal?> ToHLC3(this IEnumerable<HLC> source) => source.Select(x => (x.High + x.Low + x.Close) / 3M);

    public static IEnumerable<decimal?> ToHLC3(this IEnumerable<OHLCV> source) => source.Select(x => (x.High + x.Low + x.Close) / 3M);
}