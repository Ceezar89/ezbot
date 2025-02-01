using EzBot.Models;
using EzBot.Models.Indicator;

namespace EzBot.Core.Indicator;

public class AtrBands(AtrBandsParameter parameter) : IRiskManagementIndicator
{
    // Inputs
    private int ATRPeriod { get; set; } = parameter.Period;
    private double ATRMultiplierUpper { get; set; } = parameter.MultiplierUpper;
    private double ATRMultiplierLower { get; set; } = parameter.MultiplierLower;

    // Data sources for upper and lower calculations
    private List<double> SrcUpper { get; set; } = [];
    private List<double> SrcLower { get; set; } = [];

    // ATR values
    private List<double> ATRValues = [];

    // Upper and Lower Bands
    private List<double> UpperBand = [];
    private List<double> LowerBand = [];

    // Method to calculate ATR Bands
    public void Calculate(List<BarData> bars)
    {
        int count = bars.Count;

        // Ensure we have enough data
        if (count < ATRPeriod)
        {
            throw new ArgumentException("Not enough data to calculate ATR Bands.");
        }

        // Initialize lists
        ATRValues = [.. new double[count]];
        UpperBand = [.. new double[count]];
        LowerBand = [.. new double[count]];

        // If SrcUpper and SrcLower are not provided, default to close prices
        if (SrcUpper.Count == 0)
        {
            foreach (var bar in bars)
            {
                SrcUpper.Add(bar.Close);
            }
        }

        if (SrcLower.Count == 0)
        {
            foreach (var bar in bars)
            {
                SrcLower.Add(bar.Close);
            }
        }

        // Calculate True Range (TR) and ATR
        List<double> trueRange = [];

        for (int i = 0; i < count; i++)
        {
            if (i == 0)
            {
                // First TR is High - Low
                double tr = bars[i].High - bars[i].Low;
                trueRange.Add(tr);
            }
            else
            {
                double tr = Math.Max(
                    bars[i].High - bars[i].Low,
                    Math.Max(
                        Math.Abs(bars[i].High - bars[i - 1].Close),
                        Math.Abs(bars[i].Low - bars[i - 1].Close)
                    )
                );
                trueRange.Add(tr);
            }

            // Calculate ATR using Wilder's Moving Average
            if (i == 0)
            {
                ATRValues[i] = trueRange[i];
            }
            else if (i < ATRPeriod)
            {
                // Simple average for initial ATRPeriod
                double sumTR = 0.0;
                for (int j = 0; j <= i; j++)
                {
                    sumTR += trueRange[j];
                }
                ATRValues[i] = sumTR / (i + 1);
            }
            else
            {
                // Wilder's smoothing
                ATRValues[i] = ((ATRValues[i - 1] * (ATRPeriod - 1)) + trueRange[i]) / ATRPeriod;
            }

            // Calculate Upper and Lower Bands
            UpperBand[i] = SrcUpper[i] + ATRValues[i] * ATRMultiplierUpper;
            LowerBand[i] = SrcLower[i] - ATRValues[i] * ATRMultiplierLower;
        }
    }

    public double GetLongStopLoss()
    {
        return LowerBand.Last();
    }

    public double GetShortStopLoss()
    {
        return UpperBand.Last();
    }
}