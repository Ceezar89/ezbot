using EzBot.Models;

namespace EzBot.Services.Exchange;

public interface IExchangeAdapter
{
    // API endpoint methods
    string GetKlineRequestUri(CoinPair symbol, Interval interval);
    string GetOrderEndpoint();
    string GetTestOrderEndpoint();

    // Mapping methods
    string MapTradeType(TradeType tradeType);
    string MapOrderType();
}
