using EzBot.Models;
using EzBot.Core.IndicatorParameter;
using System;
using System.Collections.Generic;

namespace EzBot.Core.Indicator
{
    public class McGinleyDynamic(McGinleyDynamicParameter parameter) : IndicatorBase<McGinleyDynamicParameter>(parameter), ITrendIndicator
    {
        private readonly List<double> _mdValues = [];
        private readonly List<int> _signalBullish = [];
        private readonly List<int> _signalBearish = [];

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

            int n = Parameter.Period;

            // Initialize MD with the first close price if starting from the beginning
            if (startIndex == 0)
            {
                double md = bars[0].Close;
                _mdValues[0] = md;
                _signalBullish[0] = 0;
                _signalBearish[0] = 0;
                RecordProcessed(bars[0].TimeStamp, 0);
                startIndex = 1; // Start processing from the second bar
            }

            // Calculate McGinley Dynamic values for each bar
            for (int i = startIndex; i < bars.Count; i++)
            {
                double price = bars[i].Close;
                double prevMd = _mdValues[i - 1];

                // McGinley Dynamic formula: MD = MD[1] + (Price - MD[1]) / (N × (Price/MD[1])^4)
                double ratio = price / prevMd;
                double divisor = n * Math.Pow(ratio, 4);

                double md = prevMd + (price - prevMd) / divisor;

                if (i < _mdValues.Count)
                    _mdValues[i] = md;
                else
                    _mdValues.Add(md);

                // Determine signals based on price vs MD value and direction
                double currentPrice = bars[i].Close;
                double prevPrice = bars[i - 1].Close;

                bool isBullish = currentPrice > md && currentPrice > prevPrice;
                bool isBearish = currentPrice < md && currentPrice < prevPrice;

                if (i < _signalBullish.Count)
                {
                    _signalBullish[i] = isBullish ? 1 : 0;
                    _signalBearish[i] = isBearish ? 1 : 0;
                }
                else
                {
                    _signalBullish.Add(isBullish ? 1 : 0);
                    _signalBearish.Add(isBearish ? 1 : 0);
                }

                // Record this timestamp as processed using base class method
                RecordProcessed(bars[i].TimeStamp, i);
            }
        }

        private void EnsureCapacity(int capacity)
        {
            if (_mdValues.Count > 0 && _mdValues.Count < capacity)
            {
                int toAdd = capacity - _mdValues.Count;
                for (int i = 0; i < toAdd; i++)
                {
                    _mdValues.Add(0);
                    _signalBullish.Add(0);
                    _signalBearish.Add(0);
                }
            }
            else if (_mdValues.Count == 0 && capacity > 0)
            {
                // Initialize with zeros
                for (int i = 0; i < capacity; i++)
                {
                    _mdValues.Add(0);
                    _signalBullish.Add(0);
                    _signalBearish.Add(0);
                }
            }
        }

        public TrendSignal GetTrendSignal()
        {
            if (_signalBullish.Count == 0 || _signalBearish.Count == 0)
                return TrendSignal.Neutral;

            if (_signalBullish[^1] == 1)
                return TrendSignal.Bullish;

            if (_signalBearish[^1] == 1)
                return TrendSignal.Bearish;

            return TrendSignal.Neutral;
        }
    }
}