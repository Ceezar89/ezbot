using EzBot.Models;

namespace EzBot.Services.Exchange;

public class BinanceAdapter : IExchangeAdapter
{
    private string BaseUrl { get; set; } = "https://fapi.binance.com";
    private string KlineEndpoint { get; set; } = "/fapi/v1/klines";
    private static string MapSymbol(CoinPair symbol) => symbol switch
    {
        CoinPair.BTCUSDT => "BTCUSDT",
        CoinPair.ETHUSDT => "ETHUSDT",
        CoinPair.XRPUSDT => "XRPUSDT",
        CoinPair.LTCUSDT => "LTCUSDT",
        _ => throw new ArgumentOutOfRangeException(nameof(symbol))
    };

    private static string MapInterval(Interval interval) => interval switch
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

    public string GetKlineRequestUri(CoinPair symbol, Interval interval) =>
        $"{BaseUrl}{KlineEndpoint}?symbol={MapSymbol(symbol)}&interval={MapInterval(interval)}";


    // TODO: Implement trade endpoint
    // public string GetTradeEndpoint() => "/fapi/v1/order";
}
