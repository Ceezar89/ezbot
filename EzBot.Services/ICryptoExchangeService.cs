using EzBot.Models;

namespace EzBot.Services;

public interface ICryptoExchangeService
{
    Task<List<BarData>> GetBarDataAsync(CoinPair symbol, Interval interval, CancellationToken cancellationToken);
    Task<bool> ExecuteTradeAsync(string symbol, double quantity, TradeType tradeType, CancellationToken cancellationToken);
}