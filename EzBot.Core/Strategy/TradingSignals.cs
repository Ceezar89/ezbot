
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

    // Add helper methods to analyze signals
    public bool HasVolumeSignals => VolumeSignals.Count > 0;
    public bool AllVolumeHigh => HasVolumeSignals && VolumeSignals.All(v => v == VolumeSignal.High);
    public bool AllTrendsBullish => TrendSignals.Count > 0 && TrendSignals.All(t => t == TrendSignal.Bullish);
    public bool AllTrendsBearish => TrendSignals.Count > 0 && TrendSignals.All(t => t == TrendSignal.Bearish);

    // Optional: Add majority rule methods
    public bool MajorityTrendIsBullish => TrendSignals.Count > 0 &&
        TrendSignals.Count(t => t == TrendSignal.Bullish) > TrendSignals.Count / 2;

    public bool MajorityTrendIsBearish => TrendSignals.Count > 0 &&
        TrendSignals.Count(t => t == TrendSignal.Bearish) > TrendSignals.Count / 2;

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
