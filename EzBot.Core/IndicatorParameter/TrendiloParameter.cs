namespace EzBot.Core.IndicatorParameter;

public class TrendiloParameter : IIndicatorParameter
{
    public string Name { get; set; } = "trendilo";
    public int Smoothing { get; set; } = 1;
    public int Lookback { get; set; } = 50;
    public double AlmaOffset { get; set; } = 0.85;
    public int AlmaSigma { get; set; } = 6;
    public double BandMultiplier { get; set; } = 1.0;

    // Ranges
    private (int Min, int Max) SmoothingRange = (1, 10);
    private (int Min, int Max) LookbackRange = (20, 200);
    private (double Min, double Max) AlmaOffsetRange = (0.1, 2.0);
    private (int Min, int Max) AlmaSigmaRange = (1, 20);
    private (double Min, double Max) BandMultiplierRange = (0.5, 2.0);

    // Steps
    private const int SmoothingRangeStep = 1;
    private const int LookbackRangeStep = 10;
    private const double AlmaOffsetRangeStep = 0.1;
    private const int AlmaSigmaRangeStep = 1;
    private const double BandMultiplierRangeStep = 0.1;

    public TrendiloParameter()
    {
        Name = "trendilo";
        Smoothing = SmoothingRange.Min;
        Lookback = LookbackRange.Min;
        AlmaOffset = AlmaOffsetRange.Min;
        AlmaSigma = AlmaSigmaRange.Min;
        BandMultiplier = BandMultiplierRange.Min;
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

    public void IncrementSingle()
    {
        if (Smoothing < SmoothingRange.Max)
        {
            Smoothing += SmoothingRangeStep;
        }
        else if (Lookback < LookbackRange.Max)
        {
            Lookback += LookbackRangeStep;
        }
        else if (AlmaOffset < AlmaOffsetRange.Max)
        {
            AlmaOffset += AlmaOffsetRangeStep;
        }
        else if (AlmaSigma < AlmaSigmaRange.Max)
        {
            AlmaSigma += AlmaSigmaRangeStep;
        }
        else if (BandMultiplier < BandMultiplierRange.Max)
        {
            BandMultiplier += BandMultiplierRangeStep;
        }
    }

    public bool CanIncrement()
    {
        return Smoothing < SmoothingRange.Max || Lookback < LookbackRange.Max || AlmaOffset < AlmaOffsetRange.Max || AlmaSigma < AlmaSigmaRange.Max || BandMultiplier < BandMultiplierRange.Max;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Smoothing, Lookback, AlmaOffset, AlmaSigma, BandMultiplier);
    }

    public bool Equals(IIndicatorParameter? other)
    {
        if (other == null || GetType() != other.GetType())
        {
            return false;
        }

        var p = (TrendiloParameter)other;
        return (Name == p.Name) && (Smoothing == p.Smoothing) && (Lookback == p.Lookback) && (AlmaOffset == p.AlmaOffset) && (AlmaSigma == p.AlmaSigma) && (BandMultiplier == p.BandMultiplier);
    }
}