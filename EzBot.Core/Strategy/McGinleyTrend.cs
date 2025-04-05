using EzBot.Core.Indicator;
using EzBot.Models;

namespace EzBot.Core.Strategy;

public sealed class McGinleyTrend(IndicatorCollection Indicators) : TradingStrategyBase(Indicators)
{
    protected override TradeType ExecuteStrategy()
    {
        // Trade Strategy:
        // If all trend signals are bullish, go long
        // If all trend signals are bearish, go short
        // Otherwise, do not trade

        if (_signals.TrendSignals.All(t => t == TrendSignal.Bullish))
        {
            return TradeType.Long;
        }
        else if (_signals.TrendSignals.All(t => t == TrendSignal.Bearish))
        {
            return TradeType.Short;
        }
        else
        {
            return TradeType.None;
        }
    }
}
