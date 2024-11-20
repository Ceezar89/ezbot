
using System;
using System.Collections.Generic;

namespace EzBot.Core.Indicators;

public class EhlersTriangleMovingAverageIndicator
{
    // Inputs
    public bool UseETMA { get; set; } = true;
    public string SignalTypeETMA { get; set; } = "Very Strong Signal";
    public int LengthETMA { get; set; } = 14;
    public bool BarETMA { get; set; } = false;
    public bool RepETMA { get; set; } = false;

    // Internal variables
    private List<double> hlcc4 = new List<double>();
    private List<double> srcETMA = new List<double>();
    private List<double> filtETMA = new List<double>();
    private List<double> sloETMA = new List<double>();
    private List<int> sigETMA = new List<int>();

    // Output signals
    public List<int> SignalEntryLongETMA = new List<int>();
    public List<int> SignalEntryShortETMA = new List<int>();

    // Constructor
    public EhlersTriangleMovingAverageIndicator(bool useETMA = true, string signalTypeETMA = "Very Strong Signal", int lengthETMA = 14, bool barETMA = false, bool repETMA = false)
    {
        UseETMA = useETMA;
        SignalTypeETMA = signalTypeETMA;
        LengthETMA = lengthETMA;
        BarETMA = barETMA;
        RepETMA = repETMA;
    }

    public void Calculate(List<BarData> bars)
    {
        // Process each bar
        for (int i = 0; i < bars.Count; i++)
        {
            // Calculate hlcc4
            double hlcc4_i = (bars[i].High + bars[i].Low + bars[i].Close + bars[i].Close) / 4.0;
            hlcc4.Add(hlcc4_i);

            // Determine srcETMA
            double srcETMA_i;

            if (RepETMA)
            {
                srcETMA_i = GetValue(hlcc4, i - 0);
            }
            else
            {
                if (IsRealTimeBar(i, bars.Count))
                    srcETMA_i = GetValue(hlcc4, i - 1);
                else
                    srcETMA_i = GetValue(hlcc4, i - 0);
            }
            srcETMA.Add(srcETMA_i);

            // Calculate filtETMA
            double filtETMA_i = 0.0;
            double coefETMA = 0.0;
            double l2ETMA = LengthETMA / 2.0;

            for (int iETMA = 1; iETMA <= LengthETMA; iETMA++)
            {
                double cETMA;
                if (iETMA < l2ETMA)
                    cETMA = iETMA;
                else if (iETMA > l2ETMA)
                    cETMA = LengthETMA + 1 - iETMA;
                else
                    cETMA = l2ETMA;

                double srcValue = GetValue(srcETMA, i - (iETMA - 1));
                filtETMA_i += cETMA * srcValue;
                coefETMA += cETMA;
            }

            filtETMA_i = coefETMA != 0 ? filtETMA_i / coefETMA : 0.0;
            filtETMA.Add(filtETMA_i);

            // Calculate sloETMA
            double sloETMA_i = srcETMA_i - filtETMA_i;
            sloETMA.Add(sloETMA_i);

            // Calculate sigETMA
            double prevSloETMA = GetValue(sloETMA, i - 1, 0.0);

            int sigETMA_i;

            if (sloETMA_i > 0)
            {
                if (sloETMA_i > prevSloETMA)
                    sigETMA_i = 2;
                else
                    sigETMA_i = 1;
            }
            else if (sloETMA_i < 0)
            {
                if (sloETMA_i < prevSloETMA)
                    sigETMA_i = -2;
                else
                    sigETMA_i = -1;
            }
            else
            {
                sigETMA_i = 0;
            }

            sigETMA.Add(sigETMA_i);

            // Determine signalEntryLongETMA and signalEntryShortETMA
            bool signalEntryLongETMA_i;
            bool signalEntryShortETMA_i;

            if (UseETMA)
            {
                if (SignalTypeETMA == "Very Strong Signal")
                {
                    signalEntryLongETMA_i = sigETMA_i > 1 && bars[i].Close > filtETMA_i;
                    signalEntryShortETMA_i = sigETMA_i < -1 && bars[i].Close < filtETMA_i;
                }
                else if (SignalTypeETMA == "Strong Signal")
                {
                    signalEntryLongETMA_i = sigETMA_i > 0 && bars[i].Close > filtETMA_i;
                    signalEntryShortETMA_i = sigETMA_i < 0 && bars[i].Close < filtETMA_i;
                }
                else // Signal
                {
                    signalEntryLongETMA_i = bars[i].Close > filtETMA_i;
                    signalEntryShortETMA_i = bars[i].Close < filtETMA_i;
                }
            }
            else
            {
                signalEntryLongETMA_i = true;
                signalEntryShortETMA_i = true;
            }

            SignalEntryLongETMA.Add(signalEntryLongETMA_i ? 1 : 0);
            SignalEntryShortETMA.Add(signalEntryShortETMA_i ? -1 : 0);

        }
    }

    private double GetValue(List<double> list, int index, double defaultValue = 0.0)
    {
        if (index >= 0 && index < list.Count)
            return list[index];
        else
            return defaultValue;
    }

    private bool IsRealTimeBar(int currentIndex, int totalBars)
    {
        // Simulates Pine Script's barstate.isrealtime
        return currentIndex == totalBars - 1;
    }
}


