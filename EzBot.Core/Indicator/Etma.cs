using EzBot.Models;
using EzBot.Core.IndicatorParameter;

namespace EzBot.Core.Indicator
{
    public class Etma(EtmaParameter parameter)
        : IndicatorBase<EtmaParameter>(parameter), ITrendIndicator
    {
        private readonly List<int> _signalEntryLongETMA = [];
        private readonly List<int> _signalEntryShortETMA = [];
        private readonly List<double> _filtETMA = [];
        private readonly List<double> _sloETMA = [];
        private readonly List<int> _sigETMA = [];

        protected override void ProcessBarData(List<BarData> bars)
        {
            if (bars.Count == 0)
                return;

            // Resize all lists if needed
            EnsureCapacity(bars.Count);

            // Find the first bar we need to process using base class method
            int startIndex = FindStartIndex(bars);

            // If all bars have been processed, we can return
            if (startIndex >= bars.Count)
                return;

            double l2ETMA = Parameter.Length / 2.0;

            for (int i = startIndex; i < bars.Count; i++)
            {
                double srcETMA = (bars[i].High + bars[i].Low + bars[i].Close + bars[i].Close) / 4.0;
                double filtETMA_i = 0.0, coefETMA = 0.0;

                for (int j = 1; j <= Parameter.Length; j++)
                {
                    int idx = i - j + 1;
                    if (idx < 0) break;
                    double cETMA = (j < l2ETMA) ? j : (j > l2ETMA ? Parameter.Length + 1 - j : l2ETMA);
                    double srcETMA_idx = (bars[idx].High + bars[idx].Low + bars[idx].Close + bars[idx].Close) / 4.0;
                    filtETMA_i += cETMA * srcETMA_idx;
                    coefETMA += cETMA;
                }
                if (coefETMA != 0)
                    filtETMA_i /= coefETMA;

                // Update or add to the list
                if (i < _filtETMA.Count)
                    _filtETMA[i] = filtETMA_i;
                else
                    _filtETMA.Add(filtETMA_i);

                double sloETMA_i = srcETMA - filtETMA_i;
                if (i < _sloETMA.Count)
                    _sloETMA[i] = sloETMA_i;
                else
                    _sloETMA.Add(sloETMA_i);

                double previousSlo = (i > 0) ? _sloETMA[i - 1] : 0.0;
                int sigETMA_i = 0;
                if (sloETMA_i > 0)
                    sigETMA_i = (sloETMA_i > previousSlo) ? 2 : 1;
                else if (sloETMA_i < 0)
                    sigETMA_i = (sloETMA_i < previousSlo) ? -2 : -1;

                if (i < _sigETMA.Count)
                    _sigETMA[i] = sigETMA_i;
                else
                    _sigETMA.Add(sigETMA_i);

                bool signalEntryLong = false, signalEntryShort = false;
                switch (Parameter.SignalStrength)
                {
                    case SignalStrength.VeryStrong:
                        signalEntryLong = (sigETMA_i > 1) && (bars[i].Close > filtETMA_i);
                        signalEntryShort = (sigETMA_i < -1) && (bars[i].Close < filtETMA_i);
                        break;
                    case SignalStrength.Strong:
                        signalEntryLong = (sigETMA_i > 0) && (bars[i].Close > filtETMA_i);
                        signalEntryShort = (sigETMA_i < 0) && (bars[i].Close < filtETMA_i);
                        break;
                    case SignalStrength.Signal:
                        signalEntryLong = bars[i].Close > filtETMA_i;
                        signalEntryShort = bars[i].Close < filtETMA_i;
                        break;
                }

                if (i < _signalEntryLongETMA.Count)
                    _signalEntryLongETMA[i] = signalEntryLong ? 1 : 0;
                else
                    _signalEntryLongETMA.Add(signalEntryLong ? 1 : 0);

                if (i < _signalEntryShortETMA.Count)
                    _signalEntryShortETMA[i] = signalEntryShort ? -1 : 0;
                else
                    _signalEntryShortETMA.Add(signalEntryShort ? -1 : 0);

                // Record this timestamp as processed using base class method
                RecordProcessed(bars[i].TimeStamp, i);
            }
        }

        private void EnsureCapacity(int capacity)
        {
            if (_filtETMA.Count < capacity && _filtETMA.Count > 0)
            {
                int toAdd = capacity - _filtETMA.Count;
                for (int i = 0; i < toAdd; i++)
                {
                    _filtETMA.Add(0);
                    _sloETMA.Add(0);
                    _sigETMA.Add(0);
                    _signalEntryLongETMA.Add(0);
                    _signalEntryShortETMA.Add(0);
                }
            }
        }

        public TrendSignal GetTrendSignal()
        {
            if (_signalEntryLongETMA.Count != 0 && _signalEntryLongETMA.Last() == 1)
                return TrendSignal.Bullish;
            if (_signalEntryShortETMA.Count != 0 && _signalEntryShortETMA.Last() == -1)
                return TrendSignal.Bearish;
            return TrendSignal.Neutral;
        }
    }
}