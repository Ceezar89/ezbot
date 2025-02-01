using EzBot.Models;
using EzBot.Models.Indicator;

namespace EzBot.Core.Indicator;

// EhlersTriangleMovingAverage
public class ETMA(EtmaParameter parameter) : ITrendIndicator
{
    private readonly int _lengthETMA = parameter.Lenght;
    private readonly SignalStrength _signalStrength = parameter.SignalStrength;

    private readonly List<int> _signalEntryLongETMA = [];
    private readonly List<int> _signalEntryShortETMA = [];
    private readonly List<double> _filtETMA = [];
    private readonly List<double> _sloETMA = [];
    private readonly List<int> _sigETMA = [];

    public void Calculate(List<BarData> bars)
    {
        _filtETMA.Clear();
        _sloETMA.Clear();
        _sigETMA.Clear();
        _signalEntryLongETMA.Clear();
        _signalEntryShortETMA.Clear();

        double l2ETMA = _lengthETMA / 2.0;

        for (int i = 0; i < bars.Count; i++)
        {
            // Use (H + L + 2C) / 4 instead of close
            double srcETMA = (bars[i].High + bars[i].Low + bars[i].Close + bars[i].Close) / 4.0;

            double filtETMA_i = 0.0;
            double coefETMA = 0.0;

            // Triangular moving average weighting
            for (int j = 1; j <= _lengthETMA; j++)
            {
                int idx = i - j + 1;
                if (idx < 0) break; // not enough historical data

                double cETMA = (j < l2ETMA)
                    ? j
                    : (j > l2ETMA ? _lengthETMA + 1 - j : l2ETMA);

                double srcETMA_idx = (bars[idx].High + bars[idx].Low + bars[idx].Close + bars[idx].Close) / 4.0;
                filtETMA_i += cETMA * srcETMA_idx;
                coefETMA += cETMA;
            }

            if (coefETMA != 0)
                filtETMA_i /= coefETMA;

            _filtETMA.Add(filtETMA_i);

            // sloETMA = current source - TMA
            double sloETMA_i = srcETMA - filtETMA_i;
            _sloETMA.Add(sloETMA_i);

            // sigETMA logic
            double previousSlo = (i > 0) ? _sloETMA[i - 1] : 0.0;
            int sigETMA_i = 0;
            if (sloETMA_i > 0)
                sigETMA_i = (sloETMA_i > previousSlo) ? 2 : 1;
            else if (sloETMA_i < 0)
                sigETMA_i = (sloETMA_i < previousSlo) ? -2 : -1;

            _sigETMA.Add(sigETMA_i);

            // Decide how "strong" the signals must be
            bool signalEntryLongETMA_i = false;
            bool signalEntryShortETMA_i = false;

            switch (_signalStrength)
            {
                case SignalStrength.VeryStrong:
                    // Very Strong => sigETMA > 1 && close > TMA, or < -1 && close < TMA
                    signalEntryLongETMA_i = (sigETMA_i > 1) && (bars[i].Close > filtETMA_i);
                    signalEntryShortETMA_i = (sigETMA_i < -1) && (bars[i].Close < filtETMA_i);
                    break;

                case SignalStrength.Strong:
                    // Strong => sigETMA > 0 && close > TMA, or < 0 && close < TMA
                    signalEntryLongETMA_i = (sigETMA_i > 0) && (bars[i].Close > filtETMA_i);
                    signalEntryShortETMA_i = (sigETMA_i < 0) && (bars[i].Close < filtETMA_i);
                    break;

                case SignalStrength.Signal:
                    // Signal => just close > TMA or close < TMA
                    signalEntryLongETMA_i = bars[i].Close > filtETMA_i;
                    signalEntryShortETMA_i = bars[i].Close < filtETMA_i;
                    break;
            }

            _signalEntryLongETMA.Add(signalEntryLongETMA_i ? 1 : 0);
            _signalEntryShortETMA.Add(signalEntryShortETMA_i ? -1 : 0);
        }
    }

    public TrendSignal GetTrendSignal()
    {
        if (_signalEntryLongETMA.Any() && _signalEntryLongETMA.Last() == 1)
            return TrendSignal.Bullish;
        if (_signalEntryShortETMA.Any() && _signalEntryShortETMA.Last() == -1)
            return TrendSignal.Bearish;
        return TrendSignal.Neutral;
    }
}
