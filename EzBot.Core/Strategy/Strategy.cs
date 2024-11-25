using EzBot.Core.Indicator;
using EzBot.Models;

namespace EzBot.Core.Strategy;

public abstract class Strategy
{
    protected List<IIndicator> Indicators { get; set; } = [];
    protected List<TrendSignal> TrendSignals = [];
    protected List<VolumeSignal> VolumeSignals = [];
    protected double LongStoploss { get; set; }
    protected double ShortStoploss { get; set; }

    public Strategy(List<IndicatorParameter> parameters)
    {
        foreach (var parameter in parameters)
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

    protected void CalculateSignals(List<BarData> bars)
    {
        TrendSignals.Clear();
        VolumeSignals.Clear();

        foreach (IIndicator indicator in Indicators)
        {
            indicator.Calculate(bars);
            if (indicator is ITrendIndicator trendIndicator)
            {
                TrendSignals.Add(trendIndicator.GetTrendSignal());
            }
            else if (indicator is IVolumeIndicator volumeIndicator)
            {
                VolumeSignals.Add(volumeIndicator.GetVolumeSignal());
            }
            else if (indicator is IRiskManagementIndicator riskManagementIndicator)
            {
                LongStoploss = riskManagementIndicator.GetLongStopLoss();
                ShortStoploss = riskManagementIndicator.GetShortStopLoss();
            }
        }
    }
}