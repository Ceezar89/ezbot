using EzBot.Core.Indicator;
using EzBot.Models;

namespace EzBot.Core.Strategy;

public sealed class PrecisionTrend(IndicatorCollection Indicators) : TradingStrategyBase(Indicators)
{
    protected override TradeType ExecuteStrategy()
    {
        // Trade Strategy:
        // Only open trades if there is strong volume
        // If all indicators are bullish and volume is high, go long
        // If all indicators are bearish and volume is high, go short
        // Otherwise, do not trade

        if (_signals.TrendSignals.All(t => t == TrendSignal.Bullish)
                && _signals.VolumeSignals.All(v => v == VolumeSignal.High))
        {
            return TradeType.Long;
        }
        else if (_signals.TrendSignals.All(t => t == TrendSignal.Bearish)
                && _signals.VolumeSignals.All(v => v == VolumeSignal.High))
        {
            return TradeType.Short;
        }
        else
        {
            return TradeType.None;
        }
    }
}
