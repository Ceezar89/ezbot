using EzBot.Common;
using EzBot.Models;

namespace EzBot.Core.Indicator;

public class Trendilo : ITrendIndicator
{
    // Inputs
    private int Smooth { get; set; }
    private int Lookback { get; set; }
    private double AlmaOffset { get; set; }
    private int AlmaSigma { get; set; }
    private double BandMultiplier { get; set; }
    private double AvpchValue { get; set; }
    private double RmsValue { get; set; }

    // Constructor
    public Trendilo(int smooth = 1, int lookback = 50, double alma_offset = 0.85, int alma_sigma = 6, double bmult = 1.0)
    {
        Smooth = smooth;
        Lookback = lookback;
        AlmaOffset = alma_offset;
        AlmaSigma = alma_sigma;
        BandMultiplier = bmult;
    }

    public void Calculate(List<BarData> bars)
    {
        List<double> src = bars.Select(b => b.Close).ToList();
        int count = src.Count;

        // Calculate percent change over 'smooth' periods
        List<double> PercentageChange = new List<double>();
        for (int i = 0; i < count; i++)
        {
            if (i >= Smooth)
            {
                double change = (src[i] - src[i - Smooth]) / src[i - Smooth] * 100;
                PercentageChange.Add(change);
            }
            else
            {
                PercentageChange.Add(0.0);
            }
        }

        List<double> avpch = MovingAverages.ALMA(PercentageChange, Lookback, AlmaOffset, AlmaSigma);

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
