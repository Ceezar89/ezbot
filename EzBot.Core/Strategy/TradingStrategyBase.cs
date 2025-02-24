using EzBot.Core.Indicator;
using EzBot.Models;

namespace EzBot.Core.Strategy;

public abstract class TradingStrategyBase(IndicatorCollection Indicators) : ITradingStrategy
{
    private readonly IndicatorCollection _indicators = Indicators;
    protected readonly TradingSignals _signals = new();

    protected abstract TradeType ExecuteStrategy();

    public TradeOrder GetAction(List<BarData> bars)
    {
        ArgumentNullException.ThrowIfNull(bars);

        _indicators.UpdateAll(bars);
        _signals.Clear();

        foreach (var indicator in _indicators)
        {
            ProcessIndicator(indicator);
        }

        return ExecuteStrategy() switch
        {
            TradeType.Long => new TradeOrder(TradeType.Long, _signals.LongStopLoss, _signals.LongTakeProfit),
            TradeType.Short => new TradeOrder(TradeType.Short, _signals.ShortStopLoss, _signals.ShortTakeProfit),
            _ => new TradeOrder(TradeType.None, double.NaN, double.NaN)
        };
    }

    private void ProcessIndicator(IIndicator indicator)
    {
        switch (indicator)
        {
            case ITrendIndicator trendIndicator:
                _signals.TrendSignals.Add(trendIndicator.GetTrendSignal());
                break;
            case IVolumeIndicator volumeIndicator:
                _signals.VolumeSignals.Add(volumeIndicator.GetVolumeSignal());
                break;
            case IRiskManagementIndicator riskIndicator:
                _signals.LongStopLoss = riskIndicator.GetLongStopLoss();
                _signals.ShortStopLoss = riskIndicator.GetShortStopLoss();
                _signals.LongTakeProfit = riskIndicator.GetLongTakeProfit();
                _signals.ShortTakeProfit = riskIndicator.GetShortTakeProfit();
                break;
        }
    }
}