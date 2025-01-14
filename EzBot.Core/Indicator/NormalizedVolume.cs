using EzBot.Common;
using EzBot.Models;

namespace EzBot.Core.Indicator;

public class NormalizedVolume : IVolumeIndicator
{
    // Inputs
    private int Length { get; set; }
    private int HighVolumeThreshold { get; set; }
    private int LowVolumeThreshold { get; set; }
    private int NormalVolumeThreshold { get; set; }
    private double nVolume { get; set; }
    // Constructor
    public NormalizedVolume(int length = 50, int hv = 150, int lv = 75, int nv = 100)
    {
        Length = length;
        HighVolumeThreshold = hv;
        LowVolumeThreshold = lv;
        NormalVolumeThreshold = nv;
    }

    public void Calculate(List<BarData> bars)
    {
        // Extract volumes
        List<double> volumes = bars.Select(b => b.Volume).ToList();

        // Calculate SMA of volume
        List<double> smaVolume = MathUtility.SMA(volumes, Length);

        // Determine high, low, and normal volume. return sentiment for most recent bar
        nVolume = volumes.Last() / smaVolume.Last() * 100;

    }

    public VolumeSignal GetVolumeSignal()
    {
        if (nVolume >= HighVolumeThreshold)
        {
            return VolumeSignal.High;
        }
        else if (nVolume > NormalVolumeThreshold && nVolume < HighVolumeThreshold)
        {
            return VolumeSignal.Normal;
        }
        else if (nVolume <= LowVolumeThreshold)
        {
            return VolumeSignal.Low;
        }
        else
        {
            return VolumeSignal.Normal;
        }
    }

}