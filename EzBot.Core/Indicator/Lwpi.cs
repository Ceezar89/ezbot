using EzBot.Models;
using EzBot.Core.IndicatorParameter;
using System;
using System.Collections.Generic;

namespace EzBot.Core.Indicator
{
    public class Lwpi(LwpiParameter parameter) : IndicatorBase<LwpiParameter>(parameter), ITrendIndicator
    {
        private readonly List<double> _lwpiValues = [];
        private readonly List<bool> _longSignals = [];
        private readonly List<bool> _shortSignals = [];

        protected override void ProcessBarData(List<BarData> bars)
        {
            // Clear previous calculations
            _lwpiValues.Clear();
            _longSignals.Clear();
            _shortSignals.Clear();

            if (bars.Count == 0)
                return;

            int period = Parameter.Period;
            int smoothingPeriod = Parameter.SmoothingPeriod;
            const double middleValue = 50.0;

            // Lists to store intermediate values
            List<double> openCloseValues = new();
            List<double> atrValues = new();
            List<double> maValues = new();
            List<double> rawLwpiValues = new();

            // Calculate LWPI values
            for (int i = 0; i < bars.Count; i++)
            {
                // Calculate Open-Close
                double openClose = bars[i].Open - bars[i].Close;
                openCloseValues.Add(openClose);

                // Calculate ATR for normalization
                double trueRange = i > 0
                    ? Math.Max(Math.Max(
                        bars[i].High - bars[i].Low,
                        Math.Abs(bars[i].High - bars[i - 1].Close)),
                        Math.Abs(bars[i].Low - bars[i - 1].Close))
                    : bars[i].High - bars[i].Low;
                atrValues.Add(trueRange);

                // Calculate SMA of Open-Close
                double maSma = 0;
                if (i >= period - 1)
                {
                    double sum = 0;
                    for (int j = i - period + 1; j <= i; j++)
                    {
                        sum += openCloseValues[j];
                    }
                    maSma = sum / period;
                }
                maValues.Add(maSma);

                // Calculate ATR (SMA of True Range)
                double atrSma = 0;
                if (i >= period - 1)
                {
                    double sum = 0;
                    for (int j = i - period + 1; j <= i; j++)
                    {
                        sum += atrValues[j];
                    }
                    atrSma = sum / period;
                }

                // Calculate raw LWPI value
                double lwpiRaw = (atrSma != 0) ? 50 * maSma / atrSma + 50 : 50;
                rawLwpiValues.Add(lwpiRaw);

                // Apply smoothing (SMA)
                double lwpi = lwpiRaw;
                if (i >= smoothingPeriod - 1)
                {
                    double sum = 0;
                    for (int j = i - smoothingPeriod + 1; j <= i; j++)
                    {
                        sum += rawLwpiValues[j];
                    }
                    lwpi = sum / smoothingPeriod;
                }
                _lwpiValues.Add(lwpi);

                // Determine signals - check for crosses with the middle value (50)
                if (i > 0)
                {
                    bool crossUnder = _lwpiValues[i - 1] >= middleValue && _lwpiValues[i] < middleValue;
                    bool crossOver = _lwpiValues[i - 1] <= middleValue && _lwpiValues[i] > middleValue;

                    _longSignals.Add(crossUnder); // Long signal when LWPI crosses below 50
                    _shortSignals.Add(crossOver); // Short signal when LWPI crosses above 50
                }
                else
                {
                    _longSignals.Add(false);
                    _shortSignals.Add(false);
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