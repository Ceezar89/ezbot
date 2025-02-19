using EzBot.Core.Indicator;
using EzBot.Models;

namespace EzBot.Core.Strategy;

public abstract class TradingStrategyBase(IndicatorCollection Indicators) : ITradingStrategy
{
    private readonly IndicatorCollection Indicators = Indicators;
    protected List<TrendSignal> TrendSignals = [];
    protected List<VolumeSignal> VolumeSignals = [];

    // Every strategy should implement a trading logic
    protected abstract TradeType ExecuteStrategy();

    public TradeOrder GetAction(List<BarData> bars)
    {
        TrendSignals.Clear();
        VolumeSignals.Clear();
        double LongStoploss = Double.NaN;
        double ShortStoploss = Double.NaN;
        double LongTakeProfit = Double.NaN;
        double ShortTakeProfit = Double.NaN;

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
                    LongTakeProfit = riskManagementIndicator.GetLongTakeProfit();
                    ShortTakeProfit = riskManagementIndicator.GetShortTakeProfit();
                    break;
            }
        }

        // finally, execute the strategy
        return ExecuteStrategy() switch
        {
            TradeType.Long => new LongTradeOrder(LongStoploss, LongTakeProfit),
            TradeType.Short => new ShortTradeOrder(ShortStoploss, ShortTakeProfit),
            TradeType.None => new TradeOrder(TradeType.None, double.NaN, double.NaN),
            _ => new TradeOrder(TradeType.None, double.NaN, double.NaN)
        };
    }
}