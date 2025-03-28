using EzBot.Models;

namespace EzBot.Core.Strategy;

public interface ITradingStrategy
{
    TradeOrder GetAction(List<BarData> bars);

    // New optimized method that receives full history and current index
    TradeOrder GetAction(List<BarData> bars, int currentIndex)
    {
        // Default implementation for backward compatibility
        // Creates a sublist up to the current index
        return GetAction(bars.GetRange(0, currentIndex + 1));
    }
}