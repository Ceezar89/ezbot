using EzBot.Common;
using EzBot.Models;
using EzBot.Models.Indicator;

namespace EzBot.Core.Indicator;

public class NormalizedVolume(NormalizedVolumeParameter parameter) : IVolumeIndicator
{
    // Inputs
    private int Length { get; set; } = parameter.VolumePeriod;
    private int HighVolumeThreshold { get; set; } = parameter.HighVolume;
    private int LowVolumeThreshold { get; set; } = parameter.LowVolume;
    private int NormalVolumeThreshold { get; set; } = parameter.NormalHighVolumeRange;
    private double NVolume { get; set; }

    public void Calculate(List<BarData> bars)
    {
        // Extract volumes
        List<double> volumes = bars.Select(b => b.Volume).ToList();

        // Calculate SMA of volume
        List<double> smaVolume = MathUtility.SMA(volumes, Length);

        // Determine high, low, and normal volume. return sentiment for most recent bar
        NVolume = volumes.Last() / smaVolume.Last() * 100;

    }

    public VolumeSignal GetVolumeSignal()
    {
        if (NVolume >= HighVolumeThreshold)
        {
            return VolumeSignal.High;
        }
        else if (NVolume > NormalVolumeThreshold && NVolume < HighVolumeThreshold)
        {
            return VolumeSignal.Normal;
        }
        else if (NVolume <= LowVolumeThreshold)
        {
            return VolumeSignal.Low;
        }
        else
        {
            return VolumeSignal.Normal;
        }
    }

}