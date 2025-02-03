namespace EzBot.Models.Indicator;
public class TrendiloParameter : IIndicatorParameter
{
    public string Name { get; set; } = "trendilo";
    public int Smoothing { get; set; } = 1;
    public int Lookback { get; set; } = 50;
    public double AlmaOffset { get; set; } = 0.85;
    public int AlmaSigma { get; set; } = 6;
    public double BandMultiplier { get; set; } = 1.0;

    // Ranges
    public static (int Min, int Max) SmoothingRange => (1, 10);
    public static (int Min, int Max) LookbackRange => (20, 200);
    public static (double Min, double Max) AlmaOffsetRange => (0.1, 2.0);
    public static (int Min, int Max) AlmaSigmaRange => (1, 20);
    public static (double Min, double Max) BandMultiplierRange => (0.5, 2.0);

    // Steps
    public const int SmoothingRangeStep = 1;
    public const int LookbackRangeStep = 10;
    public const double AlmaOffsetRangeStep = 0.1;
    public const int AlmaSigmaRangeStep = 1;
    public const double BandMultiplierRangeStep = 0.1;

    public TrendiloParameter()
    {
    }

    public TrendiloParameter(string name, int smoothing, int lookback, double almaOffset, int almaSigma, double bandMultiplier)
    {
        Name = name;
        Smoothing = smoothing;
        Lookback = lookback;
        AlmaOffset = almaOffset;
        AlmaSigma = almaSigma;
        BandMultiplier = bandMultiplier;
    }
}