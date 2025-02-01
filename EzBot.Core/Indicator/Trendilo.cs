using EzBot.Common;
using EzBot.Models;
using EzBot.Models.Indicator;

namespace EzBot.Core.Indicator;

public class Trendilo(TrendiloParameter parameter) : ITrendIndicator
{
    private int Smoothing { get; set; } = parameter.Smoothing;
    private int Lookback { get; set; } = parameter.Lookback;
    private double AlmaOffset { get; set; } = parameter.AlmaOffset;
    private int AlmaSigma { get; set; } = parameter.AlmaSigma;
    private double BandMultiplier { get; set; } = parameter.BandMultiplier;
    private double AvpchValue { get; set; }
    private double RmsValue { get; set; }

    public void Calculate(List<BarData> bars)
    {
        List<double> src = bars.Select(b => b.Close).ToList();
        int count = src.Count;

        // Calculate percent change over 'Smoothing' periods
        List<double> PercentageChange = new List<double>();
        for (int i = 0; i < count; i++)
        {
            if (i >= Smoothing)
            {
                double change = (src[i] - src[i - Smoothing]) / src[i - Smoothing] * 100;
                PercentageChange.Add(change);
            }
            else
            {
                PercentageChange.Add(0.0);
            }
        }

        List<double> avpch = MathUtility.ALMA(PercentageChange, Lookback, AlmaOffset, AlmaSigma);

        List<double> rmsList = new List<double>();
        for (int i = 0; i < count; i++)
        {
            if (i >= Lookback - 1)
            {
                double sum = 0.0;
                for (int j = i - Lookback + 1; j <= i; j++)
                {
                    sum += avpch[j] * avpch[j];
                }
                double rms = BandMultiplier * Math.Sqrt(sum / Lookback);
                rmsList.Add(rms);
            }
            else
            {
                rmsList.Add(0.0);
            }
        }

        int idx = count - 1;
        AvpchValue = avpch[idx];
        RmsValue = rmsList[idx];
    }

    public TrendSignal GetTrendSignal()
    {
        if (AvpchValue > RmsValue)
        {
            return TrendSignal.Bullish;
        }
        else if (AvpchValue < -RmsValue)
        {
            return TrendSignal.Bearish;
        }
        return TrendSignal.Neutral;
    }
}
