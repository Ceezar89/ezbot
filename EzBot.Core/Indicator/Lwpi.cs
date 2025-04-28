using EzBot.Models;
using EzBot.Core.IndicatorParameter;

namespace EzBot.Core.Indicator
{
    public class Lwpi(LwpiParameter parameter)
        : IndicatorBase<LwpiParameter>(parameter), ITrendIndicator
    {
        private readonly List<double> _lwpiValues = [];
        private readonly List<bool> _longSignals = [];
        private readonly List<bool> _shortSignals = [];
        private readonly List<double> _openCloseValues = [];
        private readonly List<double> _atrValues = [];
        private readonly List<double> _maValues = [];
        private readonly List<double> _rawLwpiValues = [];

        protected override void ProcessBarData(List<BarData> bars)
        {
            if (bars.Count == 0)
                return;

            // Ensure our lists have enough capacity
            EnsureCapacity(bars.Count);

            // Find the first bar we need to process using base class method
            int startIndex = FindStartIndex(bars);

            // If all bars have been processed, we can return
            if (startIndex >= bars.Count)
                return;

            int period = Parameter.Period;
            int smoothingPeriod = Parameter.SmoothingPeriod;
            const double middleValue = 50.0;

            // Calculate LWPI values
            for (int i = startIndex; i < bars.Count; i++)
            {
                // Calculate Open-Close
                double openClose = bars[i].Open - bars[i].Close;
                if (i < _openCloseValues.Count)
                    _openCloseValues[i] = openClose;
                else
                    _openCloseValues.Add(openClose);

                // Calculate ATR for normalization
                double trueRange = i > 0
                    ? Math.Max(Math.Max(
                        bars[i].High - bars[i].Low,
                        Math.Abs(bars[i].High - bars[i - 1].Close)),
                        Math.Abs(bars[i].Low - bars[i - 1].Close))
                    : bars[i].High - bars[i].Low;

                if (i < _atrValues.Count)
                    _atrValues[i] = trueRange;
                else
                    _atrValues.Add(trueRange);

                // Calculate SMA of Open-Close
                double maSma = 0;
                if (i >= period - 1)
                {
                    double sum = 0;
                    for (int j = i - period + 1; j <= i; j++)
                    {
                        sum += _openCloseValues[j];
                    }
                    maSma = sum / period;
                }

                if (i < _maValues.Count)
                    _maValues[i] = maSma;
                else
                    _maValues.Add(maSma);

                // Calculate ATR (SMA of True Range)
                double atrSma = 0;
                if (i >= period - 1)
                {
                    double sum = 0;
                    for (int j = i - period + 1; j <= i; j++)
                    {
                        sum += _atrValues[j];
                    }
                    atrSma = sum / period;
                }

                // Calculate raw LWPI value
                double lwpiRaw = (atrSma != 0) ? 50 * maSma / atrSma + 50 : 50;

                if (i < _rawLwpiValues.Count)
                    _rawLwpiValues[i] = lwpiRaw;
                else
                    _rawLwpiValues.Add(lwpiRaw);

                // Apply smoothing (SMA)
                double lwpi = lwpiRaw;
                if (i >= smoothingPeriod - 1)
                {
                    double sum = 0;
                    for (int j = i - smoothingPeriod + 1; j <= i; j++)
                    {
                        sum += _rawLwpiValues[j];
                    }
                    lwpi = sum / smoothingPeriod;
                }

                if (i < _lwpiValues.Count)
                    _lwpiValues[i] = lwpi;
                else
                    _lwpiValues.Add(lwpi);

                // Determine signals - check for crosses with the middle value (50)
                if (i > 0)
                {
                    bool crossUnder = _lwpiValues[i - 1] >= middleValue && _lwpiValues[i] < middleValue;
                    bool crossOver = _lwpiValues[i - 1] <= middleValue && _lwpiValues[i] > middleValue;

                    if (i < _longSignals.Count)
                    {
                        _longSignals[i] = crossUnder; // Long signal when LWPI crosses below 50
                        _shortSignals[i] = crossOver; // Short signal when LWPI crosses above 50
                    }
                    else
                    {
                        _longSignals.Add(crossUnder);
                        _shortSignals.Add(crossOver);
                    }
                }
                else if (i < _longSignals.Count)
                {
                    _longSignals[i] = false;
                    _shortSignals[i] = false;
                }
                else
                {
                    _longSignals.Add(false);
                    _shortSignals.Add(false);
                }

                // Record this timestamp as processed using base class method
                RecordProcessed(bars[i].TimeStamp, i);
            }
        }

        private void EnsureCapacity(int capacity)
        {
            if (_lwpiValues.Count > 0 && _lwpiValues.Count < capacity)
            {
                int toAdd = capacity - _lwpiValues.Count;

                for (int i = 0; i < toAdd; i++)
                {
                    _lwpiValues.Add(0);
                    _longSignals.Add(false);
                    _shortSignals.Add(false);
                    _openCloseValues.Add(0);
                    _atrValues.Add(0);
                    _maValues.Add(0);
                    _rawLwpiValues.Add(0);
                }
            }
        }

        public TrendSignal GetTrendSignal()
        {
            if (_longSignals.Count == 0 || _shortSignals.Count == 0)
                return TrendSignal.Neutral;

            if (_longSignals[^1])
                return TrendSignal.Bullish;

            if (_shortSignals[^1])
                return TrendSignal.Bearish;

            return TrendSignal.Neutral;
        }
    }
}