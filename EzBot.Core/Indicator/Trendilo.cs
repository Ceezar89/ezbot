using EzBot.Common;
using EzBot.Models;
using EzBot.Core.IndicatorParameter;

namespace EzBot.Core.Indicator;

public class Trendilo(TrendiloParameter parameter) : IndicatorBase<TrendiloParameter>(parameter), ITrendIndicator
{
    private double AvpchValue;
    private double RmsValue;

    public override void Calculate(List<BarData> bars)
    {
        List<double> src = [.. bars.Select(b => b.Close)];
        int count = src.Count;

        List<double> percentageChange = [];
        for (int i = 0; i < count; i++)
        {
            if (i >= Parameter.Smoothing)
            {
                double change = (src[i] - src[i - Parameter.Smoothing]) / src[i - Parameter.Smoothing] * 100;
                percentageChange.Add(change);
            }
            else
            {
                percentageChange.Add(0.0);
            }
        }

        List<double> avpch = MathUtility.ALMA(percentageChange, Parameter.Lookback, Parameter.AlmaOffset, Parameter.AlmaSigma);

        List<double> rmsList = [];
        for (int i = 0; i < count; i++)
        {
            if (i >= Parameter.Lookback - 1)
            {
                double sum = 0.0;
                for (int j = i - Parameter.Lookback + 1; j <= i; j++)
                {
                    sum += avpch[j] * avpch[j];
                }
                double rms = Parameter.BandMultiplier * Math.Sqrt(sum / Parameter.Lookback);
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
