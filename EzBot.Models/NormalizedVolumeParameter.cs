namespace EzBot.Models;

public class NormalizedVolumeParameter : Parameter
{
    public int VolumePeriod { get; set; }
    public int HighVolume { get; set; }
    public int LowVolume { get; set; }
    public int NormalHighVolumeRange { get; set; }

    public NormalizedVolumeParameter(string id, int volumePeriod, int highVolume, int lowVolume, int normalHighVolumeRange)
    {
        Id = id;
        VolumePeriod = volumePeriod;
        HighVolume = highVolume;
        LowVolume = lowVolume;
        NormalHighVolumeRange = normalHighVolumeRange;
    }
}
