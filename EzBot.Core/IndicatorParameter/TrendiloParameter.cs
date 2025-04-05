namespace EzBot.Core.IndicatorParameter;

public class TrendiloParameter : IndicatorParameterBase
{
    private int _smoothing = 2;
    private int _lookback = 40;
    private double _almaOffset = 0.8;
    private int _almaSigma = 6;
    private double _bandMultiplier = 1.0;

    // Ranges
    private static readonly (int Min, int Max) SmoothingRange = (1, 2);
    private static readonly (int Min, int Max) LookbackRange = (40, 60);
    private static readonly (double Min, double Max) AlmaOffsetRange = (0.9, 1.0);
    private static readonly (int Min, int Max) AlmaSigmaRange = (10, 11);
    private static readonly (double Min, double Max) BandMultiplierRange = (2.0, 2.2);

    // Steps
    private const int SmoothingRangeStep = 1;
    private const int LookbackRangeStep = 10;
    private const double AlmaOffsetRangeStep = 0.1;
    private const int AlmaSigmaRangeStep = 1;
    private const double BandMultiplierRangeStep = 0.2;

    // Correctly calculate the number of steps for each parameter range
    private static readonly int SmoothingPermutations = CalculateSteps(SmoothingRange.Min, SmoothingRange.Max, SmoothingRangeStep);
    private static readonly int LookbackPermutations = CalculateSteps(LookbackRange.Min, LookbackRange.Max, LookbackRangeStep);
    private static readonly int AlmaOffsetPermutations = CalculateSteps(AlmaOffsetRange.Min, AlmaOffsetRange.Max, AlmaOffsetRangeStep);
    private static readonly int AlmaSigmaPermutations = CalculateSteps(AlmaSigmaRange.Min, AlmaSigmaRange.Max, AlmaSigmaRangeStep);
    private static readonly int BandMultiplierPermutations = CalculateSteps(BandMultiplierRange.Min, BandMultiplierRange.Max, BandMultiplierRangeStep);

    public static readonly int TotalPermutations = SmoothingPermutations * LookbackPermutations * AlmaOffsetPermutations * AlmaSigmaPermutations * BandMultiplierPermutations;

    public override int GetPermutationCount()
    {
        return TotalPermutations;
    }

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
        // Initialize to minimum values in the allowed ranges
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
        // Increment parameters one by one, starting from the least significant
        // When a parameter is successfully incremented, return immediately

        // Start with the least significant parameter (Band Multiplier)
        if (_bandMultiplier + BandMultiplierRangeStep <= BandMultiplierRange.Max + 0.00001) // Add epsilon for float comparison
        {
            _bandMultiplier += BandMultiplierRangeStep;
            return;
        }

        // Reset Band Multiplier and try to increment Alma Sigma
        _bandMultiplier = BandMultiplierRange.Min;
        if (_almaSigma + AlmaSigmaRangeStep <= AlmaSigmaRange.Max)
        {
            _almaSigma += AlmaSigmaRangeStep;
            return;
        }

        // Reset Alma Sigma and try to increment Alma Offset
        _almaSigma = AlmaSigmaRange.Min;
        if (_almaOffset + AlmaOffsetRangeStep <= AlmaOffsetRange.Max + 0.00001) // Add epsilon for float comparison
        {
            _almaOffset += AlmaOffsetRangeStep;
            return;
        }

        // Reset Alma Offset and try to increment Lookback
        _almaOffset = AlmaOffsetRange.Min;
        if (_lookback + LookbackRangeStep <= LookbackRange.Max)
        {
            _lookback += LookbackRangeStep;
            return;
        }

        // Reset Lookback and try to increment Smoothing
        _lookback = LookbackRange.Min;
        if (_smoothing + SmoothingRangeStep <= SmoothingRange.Max)
        {
            _smoothing += SmoothingRangeStep;
        }
    }

    public override bool CanIncrement()
    {
        // Check if any parameter can still be incremented
        if (_bandMultiplier + BandMultiplierRangeStep <= BandMultiplierRange.Max + 0.00001) // Add epsilon for float comparison
            return true;

        if (_almaSigma + AlmaSigmaRangeStep <= AlmaSigmaRange.Max)
            return true;

        if (_almaOffset + AlmaOffsetRangeStep <= AlmaOffsetRange.Max + 0.00001) // Add epsilon for float comparison
            return true;

        if (_lookback + LookbackRangeStep <= LookbackRange.Max)
            return true;

        if (_smoothing + SmoothingRangeStep <= SmoothingRange.Max)
            return true;

        return false;
    }

    public override void Reset()
    {
        Smoothing = SmoothingRange.Min;
        Lookback = LookbackRange.Min;
        AlmaOffset = AlmaOffsetRange.Min;
        AlmaSigma = AlmaSigmaRange.Min;
        BandMultiplier = BandMultiplierRange.Min;
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