
using EzBot.Models;

namespace EzBot.Core.Strategy;

public class TradingSignals
{
    public List<TrendSignal> TrendSignals { get; } = [];
    public List<VolumeSignal> VolumeSignals { get; } = [];
    public double LongStopLoss { get; set; } = double.NaN;
    public double ShortStopLoss { get; set; } = double.NaN;
    public double LongTakeProfit { get; set; } = double.NaN;
    public double ShortTakeProfit { get; set; } = double.NaN;

    public void Clear()
    {
        TrendSignals.Clear();
        VolumeSignals.Clear();
        LongStopLoss = double.NaN;
        ShortStopLoss = double.NaN;
        LongTakeProfit = double.NaN;
        ShortTakeProfit = double.NaN;
    }
}
