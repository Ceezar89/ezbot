using EzBot.Common;
using EzBot.Models;
using EzBot.Core.IndicatorParameter;

namespace EzBot.Core.Indicator;

public class Trendilo(TrendiloParameter parameter) : IndicatorBase<TrendiloParameter>(parameter), ITrendIndicator
{
    private readonly record struct CalculationState(double AvpchValue, double RmsValue);
    private CalculationState _state;

    protected override void ProcessBarData(List<BarData> bars)
    {
        List<double> percentageChanges = CalculatePercentageChanges(bars);
        List<double> avpch = MathUtility.ALMA(percentageChanges, Parameter.Lookback, Parameter.AlmaOffset, Parameter.AlmaSigma);
        List<double> rmsList = CalculateRmsList(avpch);

        int idx = bars.Count - 1;
        _state = new(avpch[idx], rmsList[idx]);
    }

    private List<double> CalculatePercentageChanges(List<BarData> bars)
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
        return percentageChange;
    }

    private List<double> CalculateRmsList(List<double> avpch)
    {
        int count = avpch.Count;
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
        return rmsList;
    }

    public TrendSignal GetTrendSignal() => _state switch
    {
        { AvpchValue: var a, RmsValue: var r } when a > r => TrendSignal.Bullish,
        { AvpchValue: var a, RmsValue: var r } when a < -r => TrendSignal.Bearish,
        _ => TrendSignal.Neutral
    };
}
