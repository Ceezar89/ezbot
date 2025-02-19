using EzBot.Core.Strategy;

namespace EzBot.Services.Strategy;

public interface IStrategyService
{
    ITradingStrategy CreateUnoptimizedStrategy(StrategyType strategyType);
}
