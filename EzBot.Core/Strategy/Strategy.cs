using EzBot.Core.Indicator;
using EzBot.Models;

namespace EzBot.Core.Strategy;

public abstract class Strategy(List<IIndicator> Indicators) : IStrategy
{
    private List<IIndicator> Indicators { get; set; } = Indicators;
    protected List<TrendSignal> TrendSignals = [];
    protected List<VolumeSignal> VolumeSignals = [];
    protected double LongStoploss { get; set; }
    protected double ShortStoploss { get; set; }

    public TradeOrder GetAction(List<BarData> bars)
    {
        TrendSignals.Clear();
        VolumeSignals.Clear();
        LongStoploss = -1;
        ShortStoploss = -1;

        foreach (IIndicator indicator in Indicators)
        {
            indicator.Calculate(bars);
            switch (indicator)
            {
                case ITrendIndicator trendIndicator:
                    TrendSignals.Add(trendIndicator.GetTrendSignal());
                    break;
                case IVolumeIndicator volumeIndicator:
                    VolumeSignals.Add(volumeIndicator.GetVolumeSignal());
                    break;
                case IRiskManagementIndicator riskManagementIndicator:
                    LongStoploss = riskManagementIndicator.GetLongStopLoss();
                    ShortStoploss = riskManagementIndicator.GetShortStopLoss();
                    break;
            }
        }
        TradeType action = ExecuteStrategy();
        if (action == TradeType.Long) return new TradeOrder(TradeType.Long, LongStoploss);
        else if (action == TradeType.Short) return new TradeOrder(TradeType.Short, ShortStoploss);
        else return new TradeOrder(TradeType.None, -1);
    }

    protected abstract TradeType ExecuteStrategy();
}