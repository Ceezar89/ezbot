using EzBot.Common;
using EzBot.Models;
using EzBot.Core.IndicatorParameter;

namespace EzBot.Core.Indicator;

public class Trendilo(TrendiloParameter parameter)
    : IndicatorBase<TrendiloParameter>(parameter), ITrendIndicator
{
    private readonly record struct CalculationState(double AvpchValue, double RmsValue);
    private CalculationState _state;
    private long _lastProcessedTimestamp;

    protected override void ProcessBarData(List<BarData> bars)
    {
        // Safety checks - ensure we have enough data and valid parameters
        if (bars == null || bars.Count < 2)
        {
            _state = new(0, 0); // Initialize with neutral values
            return;
        }

        // Check if we've already processed the latest bar
        var lastBar = bars[^1];
        if (IsProcessed(lastBar.TimeStamp) && lastBar.TimeStamp == _lastProcessedTimestamp)
        {
            // Already calculated for this timestamp
            return;
        }

        // Ensure we have sensible parameter values
        Parameter.Smoothing = Math.Max(1, Math.Min(Parameter.Smoothing, bars.Count - 1));
        Parameter.Lookback = Math.Max(2, Math.Min(Parameter.Lookback, bars.Count));

        // Skip processing if we still don't have enough data after validation
        if (bars.Count < Parameter.Lookback)
        {
            _state = new(0, 0);
            return;
        }

        try
        {
            List<double> percentageChanges = CalculatePercentageChanges(bars);
            List<double> avpch = MathUtility.ALMA(percentageChanges, Parameter.Lookback, Parameter.AlmaOffset, Parameter.AlmaSigma);
            List<double> rmsList = CalculateRmsList(avpch);

            // Make sure we have enough data to access the last element
            if (avpch.Count > 0 && rmsList.Count > 0)
            {
                int idx = Math.Min(bars.Count - 1, avpch.Count - 1);
                idx = Math.Min(idx, rmsList.Count - 1);
                _state = new(avpch[idx], rmsList[idx]);
            }
            else
            {
                _state = new(0, 0); // Default to neutral state
            }

            // Record this timestamp as processed using base class method
            RecordProcessed(lastBar.TimeStamp, bars.Count - 1);
            _lastProcessedTimestamp = lastBar.TimeStamp;
        }
        catch (Exception ex)
        {
            // Log the exception if logging is available
            Console.WriteLine($"Error in Trendilo indicator: {ex.Message}");
            _state = new(0, 0); // Safe fallback to neutral on error
        }
    }

    private List<double> CalculatePercentageChanges(List<BarData> bars)
    {
        // Get the close prices
        List<double> src = [.. bars.Select(b => b.Close)];
        int count = src.Count;

        // Ensure valid smoothing parameter
        int smoothing = Math.Max(1, Math.Min(Parameter.Smoothing, count - 1));

        List<double> percentageChange = new(count); // Pre-allocate capacity
        for (int i = 0; i < count; i++)
        {
            if (i >= smoothing)
            {
                // Safe index access with bounds checking
                if (i - smoothing >= 0 && i - smoothing < count && src[i - smoothing] != 0)
                {
                    double change = (src[i] - src[i - smoothing]) / src[i - smoothing] * 100;
                    percentageChange.Add(change);
                }
                else
                {
                    percentageChange.Add(0.0); // Default for invalid indices or division by zero
                }
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
        if (avpch == null || avpch.Count == 0)
        {
            return []; // Return empty list if no input data
        }

        int count = avpch.Count;
        List<double> rmsList = new(count); // Pre-allocate capacity

        // Ensure lookback is valid
        int lookback = Math.Max(1, Math.Min(Parameter.Lookback, count));

        for (int i = 0; i < count; i++)
        {
            if (i >= lookback - 1)
            {
                double sum = 0.0;
                int validPoints = 0;

                for (int j = Math.Max(0, i - lookback + 1); j <= Math.Min(i, count - 1); j++)
                {
                    sum += avpch[j] * avpch[j];
                    validPoints++;
                }

                // Avoid division by zero
                if (validPoints > 0)
                {
                    double rms = Parameter.BandMultiplier * Math.Sqrt(sum / validPoints);
                    rmsList.Add(rms);
                }
                else
                {
                    rmsList.Add(0.0);
                }
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


