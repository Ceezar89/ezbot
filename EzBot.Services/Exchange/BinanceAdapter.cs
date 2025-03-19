using EzBot.Models;

namespace EzBot.Services.Exchange;

public class BinanceAdapter : ExchangeAdapterBase
{
    // Override abstract properties with Binance-specific values
    protected override string BaseUrl => "https://fapi.binance.com";
    private string BaseUrlTestNet { get; } = "https://testnet.binancefuture.com";
    protected override string KlineEndpoint => "/fapi/v1/klines";
    protected override string OrderEndpoint => "/fapi/v1/order";
    protected override string TestOrderEndpoint => "/fapi/v1/order/test";
    protected override string QueryOrderEndpoint => "/fapi/v1/order";
    protected override string CancelOrderEndpoint => "/fapi/v1/order";
    protected override string CancelAllOrdersEndpoint => "/fapi/v1/allOpenOrders";
    protected override string LeverageEndpoint => "/fapi/v1/leverage";
    protected override string MarginTypeEndpoint => "/fapi/v1/marginType";
    protected override string PositionModeEndpoint => "/fapi/v1/positionSide/dual";
    protected override string PositionInfoEndpoint => "/fapi/v3/positionRisk";
    protected override string AccountBalanceEndpoint => "/fapi/v3/balance";

    // Override mapping methods
    protected override string MapSymbol(CoinPair symbol) => symbol switch
    {
        CoinPair.BTCUSDT => "BTCUSDT",
        CoinPair.ETHUSDT => "ETHUSDT",
        CoinPair.XRPUSDT => "XRPUSDT",
        CoinPair.LTCUSDT => "LTCUSDT",
        _ => throw new ArgumentOutOfRangeException(nameof(symbol))
    };

    protected override string MapInterval(Interval interval) => interval switch
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

    // Override the test endpoint method to use the testnet URL
    public override string GetTestOrderEndpoint() => $"{BaseUrlTestNet}{TestOrderEndpoint}";

    public override string MapTradeType(TradeType tradeType) => tradeType switch
    {
        TradeType.Long => "BUY",
        TradeType.Short => "SELL",
        _ => throw new ArgumentOutOfRangeException(nameof(tradeType), "Invalid trade type for order execution")
    };

    public override string MapOrderType() => "MARKET"; // Using market orders for simplicity

    public override string MapMarginType(string marginType) => marginType.ToUpperInvariant() switch
    {
        "ISOLATED" => "ISOLATED",
        "CROSSED" => "CROSSED",
        _ => "ISOLATED" // Default to isolated as per requirements
    };
}
