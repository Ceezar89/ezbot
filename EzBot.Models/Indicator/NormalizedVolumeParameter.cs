namespace EzBot.Models.Indicator;

public class NormalizedVolumeParameter : IIndicatorParameter
{
    public string Id { get; set; }
    public int VolumePeriod { get; set; } = 50;
    public int HighVolume { get; set; } = 150;
    public int LowVolume { get; set; } = 75;
    public int NormalHighVolumeRange { get; set; } = 100;

    public NormalizedVolumeParameter(string id, int volumePeriod, int highVolume, int lowVolume, int normalHighVolumeRange)
    {
        Id = id;
        VolumePeriod = volumePeriod;
        HighVolume = highVolume;
        LowVolume = lowVolume;
        NormalHighVolumeRange = normalHighVolumeRange;
    }

    public NormalizedVolumeParameter(string id)
    {
        Id = id;
    }
}
