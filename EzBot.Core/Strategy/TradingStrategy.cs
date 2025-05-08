using EzBot.Core.Indicator;
using EzBot.Models;

namespace EzBot.Core.Strategy;

public class TradingStrategy(IndicatorCollection indicators) : ITradingStrategy
{
    private readonly IndicatorCollection _indicators = indicators;
    private readonly TradingSignals _signals = new();

    public TradeOrder GetAction(List<BarData> bars)
    {
        ArgumentNullException.ThrowIfNull(bars);

        _indicators.UpdateAll(bars);
        _signals.Clear();

        foreach (var indicator in _indicators)
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

        bool hasVolumeSignals = _signals.HasVolumeSignals;
        bool allVolumeHigh = _signals.AllVolumeHigh;
        bool allTrendsBullish = _signals.AllTrendsBullish;
        bool allTrendsBearish = _signals.AllTrendsBearish;

        // Trade only when trends are aligned and volume confirms (if volume signals exist)
        if (allTrendsBullish && (!hasVolumeSignals || allVolumeHigh))
        {
            return new TradeOrder(TradeType.Long, _signals.LongStopLoss, _signals.LongTakeProfit);
        }
        else if (allTrendsBearish && (!hasVolumeSignals || allVolumeHigh))
        {
            return new TradeOrder(TradeType.Short, _signals.ShortStopLoss, _signals.ShortTakeProfit);
        }
        return new TradeOrder(TradeType.None, double.NaN, double.NaN);
    }
}