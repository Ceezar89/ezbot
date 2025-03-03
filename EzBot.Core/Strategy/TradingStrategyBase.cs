using EzBot.Core.Indicator;
using EzBot.Models;

namespace EzBot.Core.Strategy;

public abstract class TradingStrategyBase : ITradingStrategy
{
    private readonly IndicatorCollection _indicators;
    protected readonly TradingSignals _signals = new();

    protected TradingStrategyBase(IndicatorCollection indicators)
    {
        ArgumentNullException.ThrowIfNull(indicators);

        if (indicators.Count == 0)
            throw new ArgumentException("Indicator collection cannot be empty", nameof(indicators));

        _indicators = indicators;
    }

    protected abstract TradeType ExecuteStrategy();

    public TradeOrder GetAction(List<BarData> bars)
    {
        ArgumentNullException.ThrowIfNull(bars);

        _indicators.UpdateAll(bars); // actual work is done here
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