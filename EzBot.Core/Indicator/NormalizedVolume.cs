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
            if (bars.Count < Parameter.VolumePeriod)
            {
                NVolume = 100; // Default to normal volume if not enough data
                return;
            }

            // Take only the last VolumePeriod bars to calculate the average
            var recentBars = bars.Skip(bars.Count - Parameter.VolumePeriod).ToList();
            double averageVolume = recentBars.Average(b => b.Volume);
            double lastVolume = bars.Last().Volume;

            NVolume = (lastVolume / averageVolume) * 100;
        }

        public VolumeSignal GetVolumeSignal()
        {
            if (NVolume >= Parameter.HighVolume)
                return VolumeSignal.High;
            else if (NVolume <= Parameter.LowVolume)
                return VolumeSignal.Low;
            return VolumeSignal.Normal;
        }
    }
}