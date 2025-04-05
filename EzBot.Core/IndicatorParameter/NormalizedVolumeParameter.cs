namespace EzBot.Core.IndicatorParameter;

public class NormalizedVolumeParameter : IndicatorParameterBase
{
    private int _volumePeriod = 50;
    private int _highVolume = 150;
    private int _lowVolume = 75;
    private int _normalHighVolumeRange = 100;

    // Ranges
    private static readonly (int Min, int Max) VolumePeriodRange = (20, 30);
    private static readonly (int Min, int Max) HighVolumeRange = (90, 110);
    private static readonly (int Min, int Max) LowVolumeRange = (70, 90);
    private static readonly (int Min, int Max) NormalHighVolumeRangeRange = (70, 90);

    // Steps
    private const int VolumePeriodRangeStep = 5;
    private const int HighVolumeRangeStep = 10;
    private const int LowVolumeRangeStep = 10;
    private const int NormalHighVolumeRangeRangeStep = 10;

    // Correctly calculate the number of steps for each parameter range
    private static readonly int VolumePeriodPermutations = CalculateSteps(VolumePeriodRange.Min, VolumePeriodRange.Max, VolumePeriodRangeStep);
    private static readonly int HighVolumePermutations = CalculateSteps(HighVolumeRange.Min, HighVolumeRange.Max, HighVolumeRangeStep);
    private static readonly int LowVolumePermutations = CalculateSteps(LowVolumeRange.Min, LowVolumeRange.Max, LowVolumeRangeStep);
    private static readonly int NormalHighVolumeRangePermutations = CalculateSteps(NormalHighVolumeRangeRange.Min, NormalHighVolumeRangeRange.Max, NormalHighVolumeRangeRangeStep);

    public static readonly int TotalPermutations = VolumePeriodPermutations * HighVolumePermutations * LowVolumePermutations * NormalHighVolumeRangePermutations;

    public override int GetPermutationCount() => TotalPermutations;

    public override List<ParameterDescriptor> GetProperties()
    {
        return [
            new ParameterDescriptor(_volumePeriod, VolumePeriodRange.Min, VolumePeriodRange.Max, VolumePeriodRangeStep, "Volume Period"),
            new ParameterDescriptor(_highVolume, HighVolumeRange.Min, HighVolumeRange.Max, HighVolumeRangeStep, "High Volume"),
            new ParameterDescriptor(_lowVolume, LowVolumeRange.Min, LowVolumeRange.Max, LowVolumeRangeStep, "Low Volume"),
            new ParameterDescriptor(_normalHighVolumeRange, NormalHighVolumeRangeRange.Min, NormalHighVolumeRangeRange.Max, NormalHighVolumeRangeRangeStep, "Normal High Volume Range")
        ];
    }

    public override void UpdateFromDescriptor(ParameterDescriptor descriptor)
    {
        switch (descriptor.Name)
        {
            case "Volume Period":
                VolumePeriod = (int)descriptor.Value;
                break;
            case "High Volume":
                HighVolume = (int)descriptor.Value;
                break;
            case "Low Volume":
                LowVolume = (int)descriptor.Value;
                break;
            case "Normal High Volume Range":
                NormalHighVolumeRange = (int)descriptor.Value;
                break;
        }
    }

    public int VolumePeriod
    {
        get => _volumePeriod;
        set => ValidateAndSetValue(ref _volumePeriod, value, VolumePeriodRange);
    }

    public int HighVolume
    {
        get => _highVolume;
        set => ValidateAndSetValue(ref _highVolume, value, HighVolumeRange);
    }

    public int LowVolume
    {
        get => _lowVolume;
        set => ValidateAndSetValue(ref _lowVolume, value, LowVolumeRange);
    }

    public int NormalHighVolumeRange
    {
        get => _normalHighVolumeRange;
        set => ValidateAndSetValue(ref _normalHighVolumeRange, value, NormalHighVolumeRangeRange);
    }

    public NormalizedVolumeParameter() : base("normalized_volume")
    {
        // Always initialize to the minimum values of the range
        VolumePeriod = VolumePeriodRange.Min;
        HighVolume = HighVolumeRange.Min;
        LowVolume = LowVolumeRange.Min;
        NormalHighVolumeRange = NormalHighVolumeRangeRange.Min;
    }

    public NormalizedVolumeParameter(int volumePeriod, int highVolume, int lowVolume, int normalHighVolumeRange)
        : base("normalized_volume")
    {
        VolumePeriod = volumePeriod;
        HighVolume = highVolume;
        LowVolume = lowVolume;
        NormalHighVolumeRange = normalHighVolumeRange;
    }

    public override void IncrementSingle()
    {
        // Increment parameters one by one, starting from the least significant
        // When a parameter is successfully incremented, return immediately

        // Start with the least significant parameter
        if (_normalHighVolumeRange + NormalHighVolumeRangeRangeStep <= NormalHighVolumeRangeRange.Max)
        {
            _normalHighVolumeRange += NormalHighVolumeRangeRangeStep;
            return;
        }

        // Reset and try to increment the next parameter
        _normalHighVolumeRange = NormalHighVolumeRangeRange.Min;
        if (_lowVolume + LowVolumeRangeStep <= LowVolumeRange.Max)
        {
            _lowVolume += LowVolumeRangeStep;
            return;
        }

        // Reset and try to increment the next parameter
        _lowVolume = LowVolumeRange.Min;
        if (_highVolume + HighVolumeRangeStep <= HighVolumeRange.Max)
        {
            _highVolume += HighVolumeRangeStep;
            return;
        }

        // Reset and try to increment the final parameter
        _highVolume = HighVolumeRange.Min;
        if (_volumePeriod + VolumePeriodRangeStep <= VolumePeriodRange.Max)
        {
            _volumePeriod += VolumePeriodRangeStep;
        }
    }

    public override bool CanIncrement()
    {
        // Check if any parameter can still be incremented
        if (_normalHighVolumeRange + NormalHighVolumeRangeRangeStep <= NormalHighVolumeRangeRange.Max)
            return true;

        if (_lowVolume + LowVolumeRangeStep <= LowVolumeRange.Max)
            return true;

        if (_highVolume + HighVolumeRangeStep <= HighVolumeRange.Max)
            return true;

        if (_volumePeriod + VolumePeriodRangeStep <= VolumePeriodRange.Max)
            return true;

        return false;
    }

    public override void Reset()
    {
        VolumePeriod = VolumePeriodRange.Min;
        HighVolume = HighVolumeRange.Min;
        LowVolume = LowVolumeRange.Min;
        NormalHighVolumeRange = NormalHighVolumeRangeRange.Min;
    }

    public override NormalizedVolumeParameter DeepClone()
    {
        return new NormalizedVolumeParameter(VolumePeriod, HighVolume, LowVolume, NormalHighVolumeRange);
    }

    public override NormalizedVolumeParameter GetRandomNeighbor(Random random)
    {
        // Calculate how many steps are possible in each range
        int volumePeriodSteps = (VolumePeriodRange.Max - VolumePeriodRange.Min) / VolumePeriodRangeStep + 1;
        int highVolumeSteps = (HighVolumeRange.Max - HighVolumeRange.Min) / HighVolumeRangeStep + 1;
        int lowVolumeSteps = (LowVolumeRange.Max - LowVolumeRange.Min) / LowVolumeRangeStep + 1;
        int normalHighVolumeRangeSteps = (NormalHighVolumeRangeRange.Max - NormalHighVolumeRangeRange.Min) / NormalHighVolumeRangeRangeStep + 1;

        // Choose a random step for each parameter
        var volumePeriod = VolumePeriodRange.Min + (random.Next(volumePeriodSteps) * VolumePeriodRangeStep);
        var highVolume = HighVolumeRange.Min + (random.Next(highVolumeSteps) * HighVolumeRangeStep);
        var lowVolume = LowVolumeRange.Min + (random.Next(lowVolumeSteps) * LowVolumeRangeStep);
        var normalHighVolumeRange = NormalHighVolumeRangeRange.Min + (random.Next(normalHighVolumeRangeSteps) * NormalHighVolumeRangeRangeStep);

        return new NormalizedVolumeParameter(volumePeriod, highVolume, lowVolume, normalHighVolumeRange);
    }

    protected override int GetAdditionalHashCodeComponents()
    {
        return HashCode.Combine(VolumePeriod, HighVolume, LowVolume, NormalHighVolumeRange);
    }

    protected override bool EqualsCore(IIndicatorParameter other)
    {
        var p = (NormalizedVolumeParameter)other;
        return VolumePeriod == p.VolumePeriod
            && HighVolume == p.HighVolume
            && LowVolume == p.LowVolume
            && NormalHighVolumeRange == p.NormalHighVolumeRange;
    }
}
