using EzBot.Models;

namespace EzBot.Services.Exchange;

public interface IExchangeAdapter
{
    string GetKlineRequestUri(CoinPair symbol, Interval interval);
}
