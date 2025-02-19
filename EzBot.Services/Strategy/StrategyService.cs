using EzBot.Core.Indicator;
using EzBot.Core.Strategy;
using EzBot.Core.IndicatorParameter;
using EzBot.Persistence;

namespace EzBot.Services.Strategy;

public class StrategyService(EzBotDbContext dbContext) : IStrategyService
{
    private readonly EzBotDbContext _dbContext = dbContext;

    public ITradingStrategy CreateUnoptimizedStrategy(StrategyType strategyType)
    {
        return strategyType switch
        {
            StrategyType.PrecisionTrend => new PrecisionTrend(GetUnoptimizedIndicators(strategyType)),
            _ => throw new ArgumentException("Unknown StrategyType")
        };
    }

    public static ITradingStrategy CreateEmptyStrategy(StrategyType strategyType)
    {
        return strategyType switch
        {
            StrategyType.PrecisionTrend => new PrecisionTrend([]),
            _ => throw new ArgumentException("Unknown StrategyType")
        };
    }

    public static IndicatorCollection GetUnoptimizedIndicators(StrategyType strategyType)
    {
        IndicatorCollection indicators = [];

        switch (strategyType)
        {
            case StrategyType.PrecisionTrend:
                indicators.Add(new Trendilo(new TrendiloParameter()));
                indicators.Add(new NormalizedVolume(new NormalizedVolumeParameter()));
                indicators.Add(new AtrBands(new AtrBandsParameter()));
                indicators.Add(new Etma(new EtmaParameter()));
                break;
            default:
                throw new ArgumentException("Unknown StrategyType");
        }

        return indicators;
    }
}

