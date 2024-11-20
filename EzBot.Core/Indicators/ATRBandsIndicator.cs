namespace EzBot.Core.Indicators;

public class ATRBandsIndicator
{
    // Inputs
    public int ATRPeriod { get; set; } = 14;
    public double ATRMultiplierUpper { get; set; } = 2.0;
    public double ATRMultiplierLower { get; set; } = 2.0;

    // Data sources for upper and lower calculations
    public List<double> SrcUpper { get; set; } = new List<double>();
    public List<double> SrcLower { get; set; } = new List<double>();

    // ATR values
    private List<double> ATRValues = new List<double>();

    // Upper and Lower Bands
    public List<double> UpperBand = new List<double>();
    public List<double> LowerBand = new List<double>();

    // Constructor
    public ATRBandsIndicator(int atrPeriod = 14, double atrMultiplierUpper = 2.0, double atrMultiplierLower = 2.0)
    {
        ATRPeriod = atrPeriod;
        ATRMultiplierUpper = atrMultiplierUpper;
        ATRMultiplierLower = atrMultiplierLower;
    }

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
        ATRValues = new List<double>(new double[count]);
        UpperBand = new List<double>(new double[count]);
        LowerBand = new List<double>(new double[count]);

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
        List<double> trueRange = new List<double>();

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
}
