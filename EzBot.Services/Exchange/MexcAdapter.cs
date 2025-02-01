using EzBot.Models;

namespace EzBot.Services.Exchange;

public class MexcAdapter : IExchangeAdapter
{
    private string BaseUrl { get; set; } = "https://contract.mexc.com";
    private string KlineEndpoint { get; set; } = "/api/v1/contract/kline/";
    private static string MapSymbol(CoinPair symbol) => symbol switch
    {
        CoinPair.BTCUSDT => "BTC_USDT",
        CoinPair.ETHUSDT => "ETH_USDT",
        CoinPair.XRPUSDT => "XRP_USDT",
        CoinPair.LTCUSDT => "LTC_USDT",
        _ => throw new ArgumentOutOfRangeException(nameof(symbol))
    };

    private static string MapInterval(Interval interval) => interval switch
    {
        Interval.OneMinute => "Min1",
        Interval.ThreeMinutes => "Min3",
        Interval.FiveMinutes => "Min5",
        Interval.FifteenMinutes => "Min15",
        Interval.ThirtyMinutes => "Min30",
        Interval.OneHour => "Min60",
        Interval.TwoHours => "Hour2",
        _ => throw new ArgumentOutOfRangeException(nameof(interval))
    };

    public string GetKlineRequestUri(CoinPair symbol, Interval interval) =>
        $"{BaseUrl}{KlineEndpoint}{MapSymbol(symbol)}?interval={MapInterval(interval)}";


    // TODO: Implement trade endpoint
    // public string GetTradeEndpoint() => "/api/v3/order";
}
