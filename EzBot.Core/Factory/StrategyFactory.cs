using EzBot.Core.Indicator;
using EzBot.Core.Strategy;
using EzBot.Core.IndicatorParameter;

namespace EzBot.Core.Factory;

public static class StrategyFactory
{
    // Keep track of the last created parameters for optimization similarity checking
    public static IndicatorCollection? LastCreatedParameters { get; private set; }

    // Create a strategy based on the strategy type and given specific indicators
    public static ITradingStrategy CreateStrategy(StrategyType strategyType, IndicatorCollection indicators)
    {
        // Store the parameters
        LastCreatedParameters = indicators.DeepClone();

        return strategyType switch
        {
            StrategyType.PrecisionTrend => new PrecisionTrend(indicators),
            _ => throw new ArgumentException("Unknown StrategyType")
        };
    }
}