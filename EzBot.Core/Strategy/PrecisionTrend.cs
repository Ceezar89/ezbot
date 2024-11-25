using EzBot.Models;

namespace EzBot.Core.Strategy;

public sealed class PrecisionTrend(List<IndicatorParameter> parameters) : Strategy(parameters), IStrategy
{
    public TradeOrder GetAction(List<BarData> bars)
    {
        CalculateSignals(bars);

        if (TrendSignals.All(t => t == TrendSignal.Bullish) && VolumeSignals.All(v => v == VolumeSignal.High))
        {
            return new TradeOrder(ActionType.Long, LongStoploss);
        }
        else if (TrendSignals.All(t => t == TrendSignal.Bearish) && VolumeSignals.All(v => v == VolumeSignal.High))
        {
            return new TradeOrder(ActionType.Short, ShortStoploss);
        }
        else
        {
            return new TradeOrder(ActionType.None, -1);
        }
    }
}
