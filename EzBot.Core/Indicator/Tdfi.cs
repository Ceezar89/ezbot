using EzBot.Models;
using EzBot.Core.IndicatorParameter;

namespace EzBot.Core.Indicator
{
    public class Tdfi(TdfiParameter parameter) : IndicatorBase<TdfiParameter>(parameter), ITrendIndicator
    {
        private readonly List<double> _tdfiValues = [];
        private readonly List<bool> _signalLong = [];
        private readonly List<bool> _signalShort = [];

        protected override void ProcessBarData(List<BarData> bars)
        {
            // Clear previous calculations
            _tdfiValues.Clear();
            _signalLong.Clear();
            _signalShort.Clear();

            if (bars.Count == 0)
                return;

            int lookback = Parameter.Lookback;
            int mmaLength = Parameter.MmaLength;
            int smmaLength = Parameter.SmmaLength;
            int nLength = Parameter.NLength;
            double filterHigh = Parameter.FilterHigh;
            double filterLow = Parameter.FilterLow;
            bool useCrossConfirmation = Parameter.UseCrossConfirmation;
            bool useInverse = Parameter.UseInverse;

            // List to store MMA and SMMA values
            List<double> mmaValues = new();
            List<double> smmaValues = new();

            // Calculate MMA (Modified Moving Average) and SMMA values
            for (int i = 0; i < bars.Count; i++)
            {
                double price = bars[i].Close * 1000; // Similar to the pine script multiplying by 1000

                // Calculate MMA (EMA in this case since mmaMode is "ema")
                double mma = CalculateEma(price, mmaLength, mmaValues, i);
                mmaValues.Add(mma);

                // Calculate SMMA (EMA in this case since smmaMode is "ema")
                double smma = CalculateEma(mma, smmaLength, smmaValues, i);
                smmaValues.Add(smma);

                // Calculate TDFI
                if (i > 0)
                {
                    double impetmma = mma - mmaValues[i - 1]; // Momentum of MMA
                    double impetsmma = smma - smmaValues[i - 1]; // Momentum of SMMA
                    double divma = Math.Abs(mma - smma); // Divergence between MMA and SMMA
                    double averimpet = (impetmma + impetsmma) / 2; // Average momentum

                    // TDFI calculation
                    double tdf = Math.Pow(divma, 1) * Math.Pow(averimpet, nLength);

                    // Normalize TDFI value
                    double highestTdf = 0;
                    int startIndex = Math.Max(0, i - (lookback * nLength));
                    for (int j = startIndex; j <= i; j++)
                    {
                        if (j < _tdfiValues.Count)
                            highestTdf = Math.Max(highestTdf, Math.Abs(_tdfiValues[j]));
                    }

                    double normalizedTdf = highestTdf != 0 ? tdf / highestTdf : 0;
                    _tdfiValues.Add(normalizedTdf);

                    // Generate signals
                    bool signalLong, signalShort;

                    if (useCrossConfirmation)
                    {
                        signalLong = normalizedTdf > filterHigh && (i <= 1 || _tdfiValues[i - 2] <= filterHigh);
                        signalShort = normalizedTdf < filterLow && (i <= 1 || _tdfiValues[i - 2] >= filterLow);
                    }
                    else
                    {
                        signalLong = normalizedTdf > filterHigh;
                        signalShort = normalizedTdf < filterLow;
                    }

                    // Apply inverse if needed
                    if (useInverse)
                    {
                        // Swap signals
                        bool temp = signalLong;
                        signalLong = signalShort;
                        signalShort = temp;
                    }

                    _signalLong.Add(signalLong);
                    _signalShort.Add(signalShort);
                }
                else
                {
                    // First bar, initialize with zero
                    _tdfiValues.Add(0);
                    _signalLong.Add(false);
                    _signalShort.Add(false);
                }
            }
        }

        private static double CalculateEma(double value, int length, List<double> emaValues, int currentIndex)
        {
            if (currentIndex == 0)
                return value;

            double alpha = 2.0 / (length + 1);
            return alpha * value + (1 - alpha) * emaValues[currentIndex - 1];
        }

        public TrendSignal GetTrendSignal()
        {
            if (_signalLong.Count == 0 || _signalShort.Count == 0)
                return TrendSignal.Neutral;

            if (_signalLong[^1])
                return TrendSignal.Bullish;

            if (_signalShort[^1])
                return TrendSignal.Bearish;

            return TrendSignal.Neutral;
        }
    }
}