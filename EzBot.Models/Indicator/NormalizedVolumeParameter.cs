namespace EzBot.Models.Indicator;

public class NormalizedVolumeParameter : IIndicatorParameter
{
    public string Name { get; set; } = "normalized_volume";
    public int VolumePeriod { get; set; } = 50;
    public int HighVolume { get; set; } = 150;
    public int LowVolume { get; set; } = 75;
    public int NormalHighVolumeRange { get; set; } = 100;

    // Ranges
    public static (int Min, int Max) VolumePeriodRange => (10, 100);
    public static (int Min, int Max) HighVolumeRange => (50, 200);
    public static (int Min, int Max) LowVolumeRange => (40, 160);
    public static (int Min, int Max) NormalHighVolumeRangeRange => (40, 160);

    // Steps
    public const int VolumePeriodRangeStep = 5;
    public const int HighVolumeRangeStep = 10;
    public const int LowVolumeRangeStep = 5;
    public const int NormalHighVolumeRangeRangeStep = 5;

    public NormalizedVolumeParameter()
    {
    }

    public NormalizedVolumeParameter(string name, int volumePeriod, int highVolume, int lowVolume, int normalHighVolumeRange)
    {
        Name = name;
        VolumePeriod = volumePeriod;
        HighVolume = highVolume;
        LowVolume = lowVolume;
        NormalHighVolumeRange = normalHighVolumeRange;
    }
}
