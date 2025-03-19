using EzBot.Models;

namespace EzBot.Services.Exchange;

public interface IExchangeAdapter
{
    // API endpoint methods
    string GetKlineRequestUri(CoinPair symbol, Interval interval);
    string GetOrderEndpoint();
    string GetTestOrderEndpoint();
    string GetQueryOrderEndpoint();
    string GetCancelOrderEndpoint();
    string GetCancelAllOrdersEndpoint();
    string GetLeverageEndpoint();
    string GetMarginTypeEndpoint();
    string GetPositionModeEndpoint();
    string GetPositionInfoEndpoint();
    string GetAccountBalanceEndpoint();

    // Mapping methods
    string MapTradeType(TradeType tradeType);
    string MapOrderType();
    string MapMarginType(string marginType);
}
