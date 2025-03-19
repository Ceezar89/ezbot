using EzBot.Models;

namespace EzBot.Services.Exchange;

public class MexcAdapter : ExchangeAdapterBase
{
    // Override abstract properties with MEXC-specific values
    protected override string BaseUrl => "https://contract.mexc.com";
    protected override string KlineEndpoint => "/api/v1/contract/kline/";
    protected override string OrderEndpoint => "/api/v1/private/order/submit";
    protected override string TestOrderEndpoint => "/api/v1/private/order/test";

    // Override mapping methods
    protected override string MapSymbol(CoinPair symbol) => symbol switch
    {
        CoinPair.BTCUSDT => "BTC_USDT",
        CoinPair.ETHUSDT => "ETH_USDT",
        CoinPair.XRPUSDT => "XRP_USDT",
        CoinPair.LTCUSDT => "LTC_USDT",
        _ => throw new ArgumentOutOfRangeException(nameof(symbol))
    };

    protected override string MapInterval(Interval interval) => interval switch
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

    // Override standard URI construction for MEXC's unique URL pattern
    public override string GetKlineRequestUri(CoinPair symbol, Interval interval) =>
        $"{BaseUrl}{KlineEndpoint}{MapSymbol(symbol)}?interval={MapInterval(interval)}";

    // Implement trade type mapping for MEXC
    public override string MapTradeType(TradeType tradeType) => tradeType switch
    {
        TradeType.Long => "1", // MEXC uses "1" for BUY
        TradeType.Short => "2", // MEXC uses "2" for SELL
        _ => throw new ArgumentOutOfRangeException(nameof(tradeType), "Invalid trade type for order execution")
    };

    // Implement order type mapping for MEXC
    public override string MapOrderType() => "1"; // 1 for Market orders in MEXC

    // TODO: Implement trade endpoint
    // public string GetTradeEndpoint() => "/api/v3/order";
}
