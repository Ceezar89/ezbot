namespace EzBot.Core.IndicatorParameter;

public class TrendiloParameter : IndicatorParameterBase
{
    private int _smoothing = 2;
    private int _lookback = 40;
    private double _almaOffset = 0.8;
    private int _almaSigma = 6;
    private double _bandMultiplier = 1.0;

    // Ranges
    private static readonly (int Min, int Max) SmoothingRange = (4, 8);
    private static readonly (int Min, int Max) LookbackRange = (40, 100);
    private static readonly (double Min, double Max) AlmaOffsetRange = (0.6, 1.0);
    private static readonly (int Min, int Max) AlmaSigmaRange = (2, 8);
    private static readonly (double Min, double Max) BandMultiplierRange = (0.8, 1.0);

    // Steps
    private const int SmoothingRangeStep = 4;
    private const int LookbackRangeStep = 20;
    private const double AlmaOffsetRangeStep = 0.2;
    private const int AlmaSigmaRangeStep = 2;
    private const double BandMultiplierRangeStep = 0.1;

    public override List<ParameterDescriptor> GetProperties()
    {
        return [
            new ParameterDescriptor(_smoothing, SmoothingRange.Min, SmoothingRange.Max, SmoothingRangeStep, "Smoothing"),
            new ParameterDescriptor(_lookback, LookbackRange.Min, LookbackRange.Max, LookbackRangeStep, "Lookback"),
            new ParameterDescriptor(_almaOffset, AlmaOffsetRange.Min, AlmaOffsetRange.Max, AlmaOffsetRangeStep, "Alma Offset"),
            new ParameterDescriptor(_almaSigma, AlmaSigmaRange.Min, AlmaSigmaRange.Max, AlmaSigmaRangeStep, "Alma Sigma"),
            new ParameterDescriptor(_bandMultiplier, BandMultiplierRange.Min, BandMultiplierRange.Max, BandMultiplierRangeStep, "Band Multiplier")
        ];
    }

    public override void UpdateFromDescriptor(ParameterDescriptor descriptor)
    {
        switch (descriptor.Name)
        {
            case "Smoothing":
                Smoothing = (int)descriptor.Value;
                break;
            case "Lookback":
                Lookback = (int)descriptor.Value;
                break;
            case "Alma Offset":
                AlmaOffset = (double)descriptor.Value;
                break;
            case "Alma Sigma":
                AlmaSigma = (int)descriptor.Value;
                break;
            case "Band Multiplier":
                BandMultiplier = (double)descriptor.Value;
                break;
        }
    }

    public int Smoothing
    {
        get => _smoothing;
        set => ValidateAndSetValue(ref _smoothing, value, SmoothingRange);
    }

    public int Lookback
    {
        get => _lookback;
        set => ValidateAndSetValue(ref _lookback, value, LookbackRange);
    }

    public double AlmaOffset
    {
        get => _almaOffset;
        set => ValidateAndSetValue(ref _almaOffset, value, AlmaOffsetRange);
    }

    public int AlmaSigma
    {
        get => _almaSigma;
        set => ValidateAndSetValue(ref _almaSigma, value, AlmaSigmaRange);
    }

    public double BandMultiplier
    {
        get => _bandMultiplier;
        set => ValidateAndSetValue(ref _bandMultiplier, value, BandMultiplierRange);
    }

    public TrendiloParameter() : base("trendilo")
    {
        Smoothing = SmoothingRange.Min;
        Lookback = LookbackRange.Min;
        AlmaOffset = AlmaOffsetRange.Min;
        AlmaSigma = AlmaSigmaRange.Min;
        BandMultiplier = BandMultiplierRange.Min;
    }

    public TrendiloParameter(string name, int smoothing, int lookback, double almaOffset, int almaSigma, double bandMultiplier)
        : base(name)
    {
        Smoothing = smoothing;
        Lookback = lookback;
        AlmaOffset = almaOffset;
        AlmaSigma = almaSigma;
        BandMultiplier = bandMultiplier;
    }

    public override void IncrementSingle()
    {
        if (IncrementValue(ref _smoothing, SmoothingRangeStep, SmoothingRange))
            return;
        if (IncrementValue(ref _lookback, LookbackRangeStep, LookbackRange))
            return;
        if (IncrementValue(ref _almaOffset, AlmaOffsetRangeStep, AlmaOffsetRange))
            return;
        if (IncrementValue(ref _almaSigma, AlmaSigmaRangeStep, AlmaSigmaRange))
            return;
        IncrementValue(ref _bandMultiplier, BandMultiplierRangeStep, BandMultiplierRange);
    }

    public override bool CanIncrement()
    {
        return Smoothing < SmoothingRange.Max
            || Lookback < LookbackRange.Max
            || AlmaOffset < AlmaOffsetRange.Max
            || AlmaSigma < AlmaSigmaRange.Max
            || BandMultiplier < BandMultiplierRange.Max;
    }

    public override TrendiloParameter DeepClone()
    {
        return new TrendiloParameter(Name, Smoothing, Lookback, AlmaOffset, AlmaSigma, BandMultiplier);
    }

    public override TrendiloParameter GetRandomNeighbor(Random random)
    {
        // Calculate how many steps are possible in each range
        int smoothingSteps = (SmoothingRange.Max - SmoothingRange.Min) / SmoothingRangeStep + 1;
        int lookbackSteps = (LookbackRange.Max - LookbackRange.Min) / LookbackRangeStep + 1;
        int almaSigmaSteps = (AlmaSigmaRange.Max - AlmaSigmaRange.Min) / AlmaSigmaRangeStep + 1;
        int almaOffsetSteps = (int)Math.Floor((AlmaOffsetRange.Max - AlmaOffsetRange.Min) / AlmaOffsetRangeStep) + 1;
        int bandMultiplierSteps = (int)Math.Floor((BandMultiplierRange.Max - BandMultiplierRange.Min) / BandMultiplierRangeStep) + 1;

        // Choose a random step for each parameter
        var smoothing = SmoothingRange.Min + (random.Next(smoothingSteps) * SmoothingRangeStep);
        var lookback = LookbackRange.Min + (random.Next(lookbackSteps) * LookbackRangeStep);
        var almaSigma = AlmaSigmaRange.Min + (random.Next(almaSigmaSteps) * AlmaSigmaRangeStep);
        var almaOffset = AlmaOffsetRange.Min + (random.Next(almaOffsetSteps) * AlmaOffsetRangeStep);
        var bandMultiplier = BandMultiplierRange.Min + (random.Next(bandMultiplierSteps) * BandMultiplierRangeStep);

        return new TrendiloParameter(Name, smoothing, lookback, almaOffset, almaSigma, bandMultiplier);
    }

    protected override int GetAdditionalHashCodeComponents()
    {
        return HashCode.Combine(Smoothing, Lookback, AlmaOffset, AlmaSigma, BandMultiplier);
    }

    protected override bool EqualsCore(IIndicatorParameter other)
    {
        var p = (TrendiloParameter)other;
        return Smoothing == p.Smoothing
            && Lookback == p.Lookback
            && AlmaOffset == p.AlmaOffset
            && AlmaSigma == p.AlmaSigma
            && BandMultiplier == p.BandMultiplier;
    }
}