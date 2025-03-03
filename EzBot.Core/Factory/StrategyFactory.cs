using EzBot.Core.Indicator;
using EzBot.Core.Strategy;
using EzBot.Core.IndicatorParameter;

namespace EzBot.Core.Factory;

public static class StrategyFactory
{
    // Create a strategy based on the strategy type and given specific indicators
    public static ITradingStrategy CreateStrategy(StrategyType strategyType, IndicatorCollection indicators)
    {
        return strategyType switch
        {
            StrategyType.PrecisionTrend => new PrecisionTrend(indicators),
            _ => throw new ArgumentException("Unknown StrategyType")
        };
    }
}