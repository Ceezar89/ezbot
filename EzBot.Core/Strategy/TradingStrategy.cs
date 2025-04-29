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

        // Process indicators and update signals
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

        // If we have volume signals but they're not all high, don't trade
        if (_signals.HasVolumeSignals && !_signals.AllVolumeHigh)
        {
            return new TradeOrder(TradeType.None, double.NaN, double.NaN);
        }
        // Standard trend-following logic
        else if (_signals.AllTrendsBullish)
        {
            return new TradeOrder(TradeType.Long, _signals.LongStopLoss, _signals.LongTakeProfit);
        }
        else if (_signals.AllTrendsBearish)
        {
            return new TradeOrder(TradeType.Short, _signals.ShortStopLoss, _signals.ShortTakeProfit);
        }

        return new TradeOrder(TradeType.None, double.NaN, double.NaN);
    }
}