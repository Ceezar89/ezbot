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

    // Standard implementation of endpoint URI construction
    public virtual string GetKlineRequestUri(CoinPair symbol, Interval interval) =>
        $"{BaseUrl}{KlineEndpoint}?{BuildKlineQueryParameters(symbol, interval)}";

    // Standard implementation of order endpoint URIs
    public virtual string GetOrderEndpoint() => $"{BaseUrl}{OrderEndpoint}";

    public virtual string GetTestOrderEndpoint() => $"{BaseUrl}{TestOrderEndpoint}";

    // Abstract methods for exchange-specific mapping logic
    protected abstract string MapSymbol(CoinPair symbol);
    protected abstract string MapInterval(Interval interval);
    public abstract string MapTradeType(TradeType tradeType);
    public abstract string MapOrderType();

    // Allow custom implementations to specify how query parameters are built
    protected virtual string BuildKlineQueryParameters(CoinPair symbol, Interval interval) =>
        $"symbol={MapSymbol(symbol)}&interval={MapInterval(interval)}";
}