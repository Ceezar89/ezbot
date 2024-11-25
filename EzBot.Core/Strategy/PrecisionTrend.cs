using EzBot.Core.Indicator;
using EzBot.Models;

namespace EzBot.Core.Strategy;

public class PrecisionTrend(List<Parameter> parameters) : Strategy(parameters)
{
    private new List<IIndicator> Indicators { get; set; } = [];

    public override TradeOrder GetAction(List<BarData> bars)
    {
        foreach (IIndicator indicator in Indicators)
        {
            indicator.Calculate(bars);
        }

        List<TrendSignal> trendSignals = [];
        List<VolumeSignal> volumeSignals = [];
        double stoploss = -1;

        foreach (IIndicator indicator in Indicators)
        {
            if (indicator is ITrendIndicator trendIndicator)
            {
                trendSignals.Add(trendIndicator.GetTrendSignal());
            }
            else if (indicator is IVolumeIndicator volumeIndicator)
            {
                volumeSignals.Add(volumeIndicator.GetVolumeSignal());
            }
            else if (indicator is IRiskManagementIndicator riskManagementIndicator)
            {
                stoploss = riskManagementIndicator.GetStopLoss();
            }

        }
        if (trendSignals.All(t => t == TrendSignal.Bullish) && volumeSignals.All(v => v == VolumeSignal.High))
        {
            return new TradeOrder(ActionType.Long, stoploss);
        }
        else if (trendSignals.All(t => t == TrendSignal.Bearish) && volumeSignals.All(v => v == VolumeSignal.High))
        {
            return new TradeOrder(ActionType.Short, stoploss);
        }
        else
        {
            return new TradeOrder(ActionType.None, stoploss);
        }
    }

    protected override void LoadIndicators(List<Parameter> parameters)
    {
        foreach (Parameter parameter in parameters)
        {
            switch (parameter.Id)
            {
                case "trendilo":
                    TrendiloParameter trendiloParameter = (TrendiloParameter)parameter;
                    Indicators.Add(new Trendilo(trendiloParameter.SmoothTrending, trendiloParameter.Lookback, trendiloParameter.AlmaOffsetTrend, trendiloParameter.AlmaSigma, trendiloParameter.BandMultiplier));
                    break;
                case "etma":
                    ETMAParameter etmaParameter = (ETMAParameter)parameter;
                    Indicators.Add(new ETMA(etmaParameter.WindowSize, etmaParameter.Offset, etmaParameter.Sigma));
                    break;
                case "normalized_volume":
                    NormalizedVolumeParameter normalizedVolumeParameter = (NormalizedVolumeParameter)parameter;
                    Indicators.Add(new NormalizedVolume(normalizedVolumeParameter.VolumePeriod, normalizedVolumeParameter.HighVolume, normalizedVolumeParameter.LowVolume, normalizedVolumeParameter.NormalHighVolumeRange));
                    break;
                case "atr":
                    ATRBandsParameter atrParameter = (ATRBandsParameter)parameter;
                    Indicators.Add(new ATRBands(atrParameter.Period, atrParameter.Multiplier));
                    break;
            }
        }
    }
}
