using EzBot.Core.Indicator;
using EzBot.Models;

namespace EzBot.Core.Strategy;

public sealed class PrecisionTrend(List<IIndicator> Indicators) : Strategy(Indicators)
{
    protected override TradeType ExecuteStrategy()
    {
        if (TrendSignals.All(t => t == TrendSignal.Bullish) && VolumeSignals.All(v => v == VolumeSignal.High))
        {
            return TradeType.Long;
        }
        else if (TrendSignals.All(t => t == TrendSignal.Bearish) && VolumeSignals.All(v => v == VolumeSignal.High))
        {
            return TradeType.Short;
        }
        else
        {
            return TradeType.None;
        }
    }
}
