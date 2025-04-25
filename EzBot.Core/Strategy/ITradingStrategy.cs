using EzBot.Models;

namespace EzBot.Core.Strategy;

public interface ITradingStrategy
{
    TradeOrder GetAction(List<BarData> bars);
}