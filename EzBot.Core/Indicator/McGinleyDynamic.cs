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
            // Clear previous calculations
            _mdValues.Clear();
            _signalBullish.Clear();
            _signalBearish.Clear();

            if (bars.Count == 0)
                return;

            int n = Parameter.Period;

            // Initialize MD with the first close price
            double md = bars[0].Close;
            _mdValues.Add(md);

            // No signals for the first bar
            _signalBullish.Add(0);
            _signalBearish.Add(0);

            // Calculate McGinley Dynamic values for each bar
            for (int i = 1; i < bars.Count; i++)
            {
                double price = bars[i].Close;
                double prevMd = _mdValues[i - 1];

                // McGinley Dynamic formula: MD = MD[1] + (Price - MD[1]) / (N × (Price/MD[1])^4)
                double ratio = price / prevMd;
                double divisor = n * Math.Pow(ratio, 4);

                md = prevMd + (price - prevMd) / divisor;
                _mdValues.Add(md);

                // Determine signals based on price vs MD value and direction
                double currentPrice = bars[i].Close;
                double prevPrice = bars[i - 1].Close;

                bool isBullish = currentPrice > md && currentPrice > prevPrice;
                bool isBearish = currentPrice < md && currentPrice < prevPrice;

                _signalBullish.Add(isBullish ? 1 : 0);
                _signalBearish.Add(isBearish ? 1 : 0);
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