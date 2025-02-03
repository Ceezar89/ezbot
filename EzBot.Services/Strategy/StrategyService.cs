using EzBot.Core.Indicator;
using EzBot.Core.Strategy;
using EzBot.Models.Indicator;
using EzBot.Persistence;

namespace EzBot.Services.Strategy;

public class StrategyService(EzBotDbContext dbContext) : IStrategyService
{
    private readonly EzBotDbContext _dbContext = dbContext;

    public IStrategy CreateUnoptimizedStrategy(StrategyType strategyType)
    {
        return strategyType switch
        {
            StrategyType.PrecisionTrend => new PrecisionTrend(GetUnoptimizedIndicators(strategyType)),
            _ => throw new ArgumentException("Unknown StrategyType")
        };
    }

    private static List<IIndicator> GetUnoptimizedIndicators(StrategyType strategyType)
    {
        List<IIndicator> indicators = [];

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

