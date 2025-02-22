namespace EzBot.Core.IndicatorParameter;

public class TrendiloParameter : IndicatorParameterBase
{
    private int _smoothing = 1;
    private int _lookback = 50;
    private double _almaOffset = 0.85;
    private int _almaSigma = 6;
    private double _bandMultiplier = 1.0;

    // Ranges
    private static readonly (int Min, int Max) SmoothingRange = (1, 10);
    private static readonly (int Min, int Max) LookbackRange = (20, 200);
    private static readonly (double Min, double Max) AlmaOffsetRange = (0.1, 2.0);
    private static readonly (int Min, int Max) AlmaSigmaRange = (1, 20);
    private static readonly (double Min, double Max) BandMultiplierRange = (0.5, 2.0);

    // Steps
    private const int SmoothingRangeStep = 1;
    private const int LookbackRangeStep = 10;
    private const double AlmaOffsetRangeStep = 0.1;
    private const int AlmaSigmaRangeStep = 1;
    private const double BandMultiplierRangeStep = 0.1;

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