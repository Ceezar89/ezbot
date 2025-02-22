using EzBot.Models;
using EzBot.Core.IndicatorParameter;
using System.Diagnostics;

namespace EzBot.Core.Indicator
{
    public class NormalizedVolume(NormalizedVolumeParameter parameter) : IndicatorBase<NormalizedVolumeParameter>(parameter), IVolumeIndicator
    {
        private double NVolume;

        public override void Calculate(List<BarData> bars)
        {
            if (bars.Count < Parameter.VolumePeriod)
                throw new ArgumentException($"Not enough bars for calculation. Need at least {Parameter.VolumePeriod} bars.");

            // Take only the last VolumePeriod bars for SMA calculation
            var recentBars = bars.Skip(Math.Max(0, bars.Count - Parameter.VolumePeriod)).ToList();
            List<double> volumes = recentBars.Select(b => b.Volume).ToList();
            double smaVolume = volumes.Average(); // Simple average for the period
            double lastVolume = bars.Last().Volume;

            NVolume = (lastVolume / smaVolume) * 100;

            Debug.WriteLine($"Last Volume: {lastVolume}, SMA: {smaVolume}, NVolume: {NVolume}");
            Debug.WriteLine($"Thresholds - High: {Parameter.HighVolume}, Low: {Parameter.LowVolume}, Normal: {Parameter.NormalHighVolumeRange}");
        }

        public VolumeSignal GetVolumeSignal()
        {
            Debug.WriteLine($"Checking NVolume: {NVolume}");

            // First check if it's low volume
            if (NVolume <= Parameter.LowVolume)
            {
                Debug.WriteLine($"Volume {NVolume} <= Low threshold {Parameter.LowVolume} -> Low");
                return VolumeSignal.Low;
            }

            // Then check if it's high volume
            if (NVolume >= Parameter.HighVolume)
            {
                Debug.WriteLine($"Volume {NVolume} >= High threshold {Parameter.HighVolume} -> High");
                return VolumeSignal.High;
            }
            // Otherwise it's normal volume
            else if (NVolume <= Parameter.LowVolume)
                return VolumeSignal.Low;
            Debug.WriteLine($"Volume {NVolume} is between thresholds -> Normal");
            return VolumeSignal.Normal;
        }
    }
}