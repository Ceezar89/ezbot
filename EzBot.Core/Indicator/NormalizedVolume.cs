using EzBot.Common;
using EzBot.Models;
using EzBot.Core.IndicatorParameter;

namespace EzBot.Core.Indicator
{
    public class NormalizedVolume(NormalizedVolumeParameter parameter) : IndicatorBase<NormalizedVolumeParameter>(parameter), IVolumeIndicator
    {
        private double NVolume;

        public override void Calculate(List<BarData> bars)
        {
            List<double> volumes = bars.Select(b => b.Volume).ToList();
            List<double> smaVolume = MathUtility.SMA(volumes, Parameter.VolumePeriod);
            NVolume = volumes.Last() / smaVolume.Last() * 100;
        }

        public VolumeSignal GetVolumeSignal()
        {
            if (NVolume >= Parameter.HighVolume)
                return VolumeSignal.High;
            else if (NVolume > Parameter.NormalHighVolumeRange && NVolume < Parameter.HighVolume)
                return VolumeSignal.Normal;
            else if (NVolume <= Parameter.LowVolume)
                return VolumeSignal.Low;
            return VolumeSignal.Normal;
        }
    }
}