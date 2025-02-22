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
