using EzBot.Models;

namespace EzBot.Core.Strategy;

public interface IStrategy
{
    TradeOrder GetAction(List<BarData> bars);
}