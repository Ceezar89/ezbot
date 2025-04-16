using EzBot.Core.Indicator;
using EzBot.Core.Strategy;

namespace EzBot.Core.Factory;

public static class StrategyFactory
{
    public static ITradingStrategy CreateStrategy(StrategyType strategyType, IndicatorCollection indicators)
    {
        return strategyType switch
        {
            StrategyType.TrendsAndVolume => new PrecisionTrend(indicators),
            StrategyType.McGinleyTrend => new McGinleyTrend(indicators),
            StrategyType.EtmaTrend => new EtmaTrend(indicators),
            _ => throw new ArgumentException("Unknown StrategyType")
        };
    }
}