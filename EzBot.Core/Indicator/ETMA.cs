using EzBot.Models;

namespace EzBot.Core.Indicator;

// EhlersTriangleMovingAverage
public class ETMA : ITrendIndicator
{
    private List<int> SignalEntryLongETMA = new List<int>();
    private List<int> SignalEntryShortETMA = new List<int>();
    private List<double> filtETMA = new List<double>();
    private List<double> sloETMA = new List<double>();
    private List<int> sigETMA = new List<int>();

    public ETMA(int windowSize, double offset, double sigma)
    {
        // TODO: Implement constructor
    }

    public void Calculate(List<BarData> bars)
    {
        int lengthETMA = 14; // Example length, can be parameterized
        double l2ETMA = lengthETMA / 2.0;

        filtETMA.Clear();
        sloETMA.Clear();
        sigETMA.Clear();
        SignalEntryLongETMA.Clear();
        SignalEntryShortETMA.Clear();

        for (int i = 0; i < bars.Count; i++)
        {
            double srcETMA = bars[i].Close;
            double filtETMA_i = 0.0;
            double coefETMA = 0.0;

            for (int j = 1; j <= lengthETMA; j++)
            {
                int idx = i - j + 1;
                if (idx < 0) break;

                double cETMA = j < l2ETMA ? j : j > l2ETMA ? lengthETMA + 1 - j : l2ETMA;
                filtETMA_i += cETMA * bars[idx].Close;
                coefETMA += cETMA;
            }

            filtETMA_i = coefETMA != 0 ? filtETMA_i / coefETMA : 0;
            filtETMA.Add(filtETMA_i);

            double sloETMA_i = srcETMA - filtETMA_i;
            sloETMA.Add(sloETMA_i);

            int sigETMA_i = sloETMA_i > 0 ? (sloETMA_i > (i > 0 ? sloETMA[i - 1] : 0) ? 2 : 1) : (sloETMA_i < 0 ? (sloETMA_i < (i > 0 ? sloETMA[i - 1] : 0) ? -2 : -1) : 0);
            sigETMA.Add(sigETMA_i);

            bool signalEntryLongETMA_i = sigETMA_i > 1 && bars[i].Close > filtETMA_i;
            bool signalEntryShortETMA_i = sigETMA_i < -1 && bars[i].Close < filtETMA_i;

            SignalEntryLongETMA.Add(signalEntryLongETMA_i ? 1 : 0);
            SignalEntryShortETMA.Add(signalEntryShortETMA_i ? -1 : 0);
        }

    }

    public TrendSignal GetTrendSignal()
    {
        if (SignalEntryLongETMA.Last() == 1)
        {
            return TrendSignal.Bullish;
        }
        else if (SignalEntryShortETMA.Last() == -1)
        {
            return TrendSignal.Bearish;
        }
        else
        {
            return TrendSignal.Neutral;
        }
    }
}