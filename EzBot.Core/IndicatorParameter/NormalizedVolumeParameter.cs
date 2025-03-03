namespace EzBot.Core.IndicatorParameter;

public class NormalizedVolumeParameter : IndicatorParameterBase
{
    private int _volumePeriod = 50;
    private int _highVolume = 150;
    private int _lowVolume = 75;
    private int _normalHighVolumeRange = 100;

    // Ranges
    private static readonly (int Min, int Max) VolumePeriodRange = (10, 100);
    private static readonly (int Min, int Max) HighVolumeRange = (50, 200);
    private static readonly (int Min, int Max) LowVolumeRange = (40, 160);
    private static readonly (int Min, int Max) NormalHighVolumeRangeRange = (40, 160);

    // Steps
    private const int VolumePeriodRangeStep = 5;
    private const int HighVolumeRangeStep = 10;
    private const int LowVolumeRangeStep = 5;
    private const int NormalHighVolumeRangeRangeStep = 5;

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
        if (IncrementValue(ref _volumePeriod, VolumePeriodRangeStep, VolumePeriodRange))
            return;
        if (IncrementValue(ref _highVolume, HighVolumeRangeStep, HighVolumeRange))
            return;
        if (IncrementValue(ref _lowVolume, LowVolumeRangeStep, LowVolumeRange))
            return;
        IncrementValue(ref _normalHighVolumeRange, NormalHighVolumeRangeRangeStep, NormalHighVolumeRangeRange);
    }

    public override bool CanIncrement()
    {
        return VolumePeriod < VolumePeriodRange.Max
            || HighVolume < HighVolumeRange.Max
            || LowVolume < LowVolumeRange.Max
            || NormalHighVolumeRange < NormalHighVolumeRangeRange.Max;
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
