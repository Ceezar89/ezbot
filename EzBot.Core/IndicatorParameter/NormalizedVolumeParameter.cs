namespace EzBot.Core.IndicatorParameter;

public class NormalizedVolumeParameter : IIndicatorParameter
{
    public string Name { get; set; } = "normalized_volume";
    public int VolumePeriod { get; set; } = 50;
    public int HighVolume { get; set; } = 150;
    public int LowVolume { get; set; } = 75;
    public int NormalHighVolumeRange { get; set; } = 100;

    // Ranges
    private (int Min, int Max) VolumePeriodRange = (10, 100);
    private (int Min, int Max) HighVolumeRange = (50, 200);
    private (int Min, int Max) LowVolumeRange = (40, 160);
    private (int Min, int Max) NormalHighVolumeRangeRange = (40, 160);

    // Steps
    private const int VolumePeriodRangeStep = 5;
    private const int HighVolumeRangeStep = 10;
    private const int LowVolumeRangeStep = 5;
    private const int NormalHighVolumeRangeRangeStep = 5;

    public NormalizedVolumeParameter()
    {
        Name = "normalized_volume";
    }

    public NormalizedVolumeParameter(int volumePeriod, int highVolume, int lowVolume, int normalHighVolumeRange)
    {
        VolumePeriod = volumePeriod;
        HighVolume = highVolume;
        LowVolume = lowVolume;
        NormalHighVolumeRange = normalHighVolumeRange;
    }

    public void IncrementSingle()
    {
        if (VolumePeriod < VolumePeriodRange.Max)
        {
            VolumePeriod += VolumePeriodRangeStep;
        }
        else if (HighVolume < HighVolumeRange.Max)
        {
            HighVolume += HighVolumeRangeStep;
        }
        else if (LowVolume < LowVolumeRange.Max)
        {
            LowVolume += LowVolumeRangeStep;
        }
        else if (NormalHighVolumeRange < NormalHighVolumeRangeRange.Max)
        {
            NormalHighVolumeRange += NormalHighVolumeRangeRangeStep;
        }
    }

    public bool CanIncrement()
    {
        return VolumePeriod < VolumePeriodRange.Max || HighVolume < HighVolumeRange.Max || LowVolume < LowVolumeRange.Max || NormalHighVolumeRange < NormalHighVolumeRangeRange.Max;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, VolumePeriod, HighVolume, LowVolume, NormalHighVolumeRange);
    }

    public bool Equals(IIndicatorParameter? other)
    {
        if (other == null || GetType() != other.GetType())
        {
            return false;
        }

        var p = (NormalizedVolumeParameter)other;
        return (Name == p.Name) && (VolumePeriod == p.VolumePeriod) && (HighVolume == p.HighVolume) && (LowVolume == p.LowVolume) && (NormalHighVolumeRange == p.NormalHighVolumeRange);
    }
}
