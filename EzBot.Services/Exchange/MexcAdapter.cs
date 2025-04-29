using EzBot.Models;

namespace EzBot.Services.Exchange;

public class MexcAdapter : ExchangeAdapterBase
{
    // Override abstract properties with MEXC-specific values
    protected override string BaseUrl => "https://contract.mexc.com";
    protected override string KlineEndpoint => "/api/v1/contract/kline/";
    protected override string OrderEndpoint => "/api/v1/private/order/submit";
    protected override string TestOrderEndpoint => "/api/v1/private/order/test";
    protected override string QueryOrderEndpoint => "/api/v1/private/order/get";
    protected override string CancelOrderEndpoint => "/api/v1/private/order/cancel";
    protected override string CancelAllOrdersEndpoint => "/api/v1/private/order/cancel_all";
    protected override string LeverageEndpoint => "/api/v1/private/position/change_leverage";
    protected override string MarginTypeEndpoint => "/api/v1/private/position/isolated/switch";
    protected override string PositionModeEndpoint => "/api/v1/private/position/position_mode/change";
    protected override string PositionInfoEndpoint => "/api/v1/private/position/open_positions";
    protected override string AccountBalanceEndpoint => "/api/v1/private/account/assets";

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
        TradeType.Long => "1", // MEXC uses "1" for open long
        TradeType.Short => "3", // MEXC uses "3" for open short
        _ => throw new ArgumentOutOfRangeException(nameof(tradeType), "Invalid trade type for order execution")
    };

    // Implement order type mapping for MEXC
    public override string MapOrderType() => "5"; // 5 for Market orders in MEXC

    // Override margin type mapping for MEXC
    public override string MapMarginType(string marginType) => marginType.ToUpperInvariant() switch
    {
        "ISOLATED" => "1", // 1 for isolated margin in MEXC
        "CROSSED" => "2",  // 2 for cross margin in MEXC
        _ => "1"           // Default to isolated as per requirements
    };

    // TODO: Implement trade endpoint
    // public string GetTradeEndpoint() => "/api/v3/order";
}
