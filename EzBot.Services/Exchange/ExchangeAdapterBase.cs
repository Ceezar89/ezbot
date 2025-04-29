using EzBot.Models;

namespace EzBot.Services.Exchange;

/// <summary>
/// Abstract base class for exchange adapters that standardizes common functionality
/// and enforces consistent implementation across different exchange integrations.
/// </summary>
public abstract class ExchangeAdapterBase : IExchangeAdapter
{
    // Abstract properties that define exchange-specific endpoints
    protected abstract string BaseUrl { get; }
    protected abstract string KlineEndpoint { get; }
    protected abstract string OrderEndpoint { get; }
    protected abstract string TestOrderEndpoint { get; }
    protected abstract string QueryOrderEndpoint { get; }
    protected abstract string CancelOrderEndpoint { get; }
    protected abstract string CancelAllOrdersEndpoint { get; }
    protected abstract string LeverageEndpoint { get; }
    protected abstract string MarginTypeEndpoint { get; }
    protected abstract string PositionModeEndpoint { get; }
    protected abstract string PositionInfoEndpoint { get; }
    protected abstract string AccountBalanceEndpoint { get; }

    // Standard implementation of endpoint URI construction
    public virtual string GetKlineRequestUri(CoinPair symbol, Interval interval) =>
        $"{BaseUrl}{KlineEndpoint}?{BuildKlineQueryParameters(symbol, interval)}";

    // Standard implementation of order endpoint URIs
    public virtual string GetOrderEndpoint() => $"{BaseUrl}{OrderEndpoint}";
    public virtual string GetTestOrderEndpoint() => $"{BaseUrl}{TestOrderEndpoint}";
    public virtual string GetQueryOrderEndpoint() => $"{BaseUrl}{QueryOrderEndpoint}";
    public virtual string GetCancelOrderEndpoint() => $"{BaseUrl}{CancelOrderEndpoint}";
    public virtual string GetCancelAllOrdersEndpoint() => $"{BaseUrl}{CancelAllOrdersEndpoint}";
    public virtual string GetLeverageEndpoint() => $"{BaseUrl}{LeverageEndpoint}";
    public virtual string GetMarginTypeEndpoint() => $"{BaseUrl}{MarginTypeEndpoint}";
    public virtual string GetPositionModeEndpoint() => $"{BaseUrl}{PositionModeEndpoint}";
    public virtual string GetPositionInfoEndpoint() => $"{BaseUrl}{PositionInfoEndpoint}";
    public virtual string GetAccountBalanceEndpoint() => $"{BaseUrl}{AccountBalanceEndpoint}";

    // Abstract methods for exchange-specific mapping logic
    protected abstract string MapSymbol(CoinPair symbol);
    protected abstract string MapInterval(Interval interval);
    public abstract string MapTradeType(TradeType tradeType);
    public abstract string MapOrderType();
    public virtual string MapMarginType(string marginType) => marginType.ToUpperInvariant();

    // Allow custom implementations to specify how query parameters are built
    protected virtual string BuildKlineQueryParameters(CoinPair symbol, Interval interval) =>
        $"symbol={MapSymbol(symbol)}&interval={MapInterval(interval)}";
}