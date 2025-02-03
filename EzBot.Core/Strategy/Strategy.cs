using EzBot.Core.Indicator;
using EzBot.Models;

namespace EzBot.Core.Strategy;

public abstract class Strategy(List<IIndicator> Indicators) : IStrategy
{
    private List<IIndicator> Indicators { get; set; } = Indicators;
    protected List<TrendSignal> TrendSignals = [];
    protected List<VolumeSignal> VolumeSignals = [];

    // Every strategy should implement a trading logic
    protected abstract TradeType ExecuteStrategy();

    public TradeOrder GetAction(List<BarData> bars)
    {
        TrendSignals.Clear();
        VolumeSignals.Clear();
        double LongStoploss = -1;
        double ShortStoploss = -1;

        foreach (IIndicator indicator in Indicators)
        {
            indicator.Calculate(bars); // Process the indicators

            // Get the signals from the indicators
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

        // finally, execute the strategy
        return ExecuteStrategy() switch
        {
            TradeType.Long => new TradeOrder(TradeType.Long, LongStoploss),
            TradeType.Short => new TradeOrder(TradeType.Short, ShortStoploss),
            _ => new TradeOrder(TradeType.None, -1)
        };
    }
}