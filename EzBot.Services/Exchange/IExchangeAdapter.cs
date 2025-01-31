using EzBot.Models;

namespace EzBot.Services;
public interface IExchangeAdapter
{
    string MapSymbol(CoinPair symbol);
    string MapInterval(Interval interval);
    string GetBaseUrl();
    string GetKlineEndpoint();
    // string GetTradeEndpoint();
}
