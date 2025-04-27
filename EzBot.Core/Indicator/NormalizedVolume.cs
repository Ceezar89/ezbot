using EzBot.Models;
using EzBot.Core.IndicatorParameter;
using EzBot.Common;
using System.Linq;

namespace EzBot.Core.Indicator
{
    public class NormalizedVolume(NormalizedVolumeParameter parameter) : IndicatorBase<NormalizedVolumeParameter>(parameter), IVolumeIndicator
    {
        private double NVolume;
        private long _lastProcessedTimestamp;

        protected override void ProcessBarData(List<BarData> bars)
        {
            if (bars.Count < Parameter.VolumePeriod)
                throw new ArgumentException($"Not enough bars for calculation. Need at least {Parameter.VolumePeriod} bars.");

            // Check if we've already processed the most recent bar
            var lastBar = bars.Last();
            if (IsProcessed(lastBar.TimeStamp) && lastBar.TimeStamp == _lastProcessedTimestamp)
            {
                // Already calculated for this timestamp
                return;
            }

            // Take only the last VolumePeriod bars for SMA calculation
            var recentBars = bars.Skip(Math.Max(0, bars.Count - Parameter.VolumePeriod)).ToList();
            List<double> volumes = [.. recentBars.Select(b => b.Volume)];
            List<double> smaVolumes = MathUtility.SMA(volumes, Parameter.VolumePeriod);

            NVolume = volumes.Last() / smaVolumes.Last() * 100;

            // Record this timestamp as processed using base class method
            RecordProcessed(lastBar.TimeStamp, bars.Count - 1);
            _lastProcessedTimestamp = lastBar.TimeStamp;
        }

        public VolumeSignal GetVolumeSignal()
        {
            if (NVolume <= Parameter.LowVolume)
                return VolumeSignal.Low;
            if (NVolume >= Parameter.HighVolume)
                return VolumeSignal.High;
            return VolumeSignal.Normal;
        }
    }
}