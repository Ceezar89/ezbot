using EzBot.Models;

namespace EzBot.Services.Exchange;

public class MexcAdapter : IExchangeAdapter
{
    public string MapSymbol(CoinPair symbol) => symbol switch
    {
        CoinPair.BTCUSDT => "BTC_USDT",
        CoinPair.ETHUSDT => "ETH_USDT",
        CoinPair.XRPUSDT => "XRP_USDT",
        CoinPair.LTCUSDT => "LTC_USDT",
        _ => throw new ArgumentOutOfRangeException(nameof(symbol))
    };

    public string MapInterval(Interval interval) => interval switch
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

    public string GetBaseUrl() => "https://contract.mexc.com";
    public string GetKlineEndpoint() => "/api/v1/contract/kline/";

    // TODO: Implement trade endpoint
    // public string GetTradeEndpoint() => "/api/v3/order";
}
