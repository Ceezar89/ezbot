using EzBot.Core.Strategy;

namespace EzBot.Services.Strategy;

public interface IStrategyService
{
    IStrategy CreateUnoptimizedStrategy(StrategyType strategyType);
}
