using EzBot.Models;

namespace EzBot.Services;

public class BinanceAdapter : IExchangeAdapter
{
    public string MapSymbol(CoinPair symbol) => symbol switch
    {
        CoinPair.BTCUSDT => "BTCUSDT",
        CoinPair.ETHUSDT => "ETHUSDT",
        CoinPair.XRPUSDT => "XRPUSDT",
        CoinPair.LTCUSDT => "LTCUSDT",
        _ => throw new ArgumentOutOfRangeException(nameof(symbol))
    };

    public string MapInterval(Interval interval) => interval switch
    {
        Interval.OneMinute => "1m",
        Interval.ThreeMinutes => "3m",
        Interval.FiveMinutes => "5m",
        Interval.FifteenMinutes => "15m",
        Interval.ThirtyMinutes => "30m",
        Interval.OneHour => "1h",
        Interval.TwoHours => "2h",
        _ => throw new ArgumentOutOfRangeException(nameof(interval))
    };

    public string GetBaseUrl() => "https://fapi.binance.com";
    public string GetKlineEndpoint() => "/fapi/v1/klines";
    // public string GetTradeEndpoint() => "/fapi/v1/order";
}
