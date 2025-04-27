using EzBot.Models;
using EzBot.Core.IndicatorParameter;

namespace EzBot.Core.Indicator;

public class Supertrend(SupertrendParameter parameter) : IndicatorBase<SupertrendParameter>(parameter), ITrendIndicator
{
    private readonly List<double> _upperBand = [];
    private readonly List<double> _lowerBand = [];
    private readonly List<double> _supertrendLine = [];
    private readonly List<int> _direction = []; // 1 for uptrend, -1 for downtrend
    private readonly List<double> _atrValues = [];

    protected override void ProcessBarData(List<BarData> bars)
    {
        if (bars.Count == 0)
            return;

        // Resize our collections if needed
        EnsureCapacity(bars.Count);

        // Find the first bar we need to process using base class method
        int startIndex = FindStartIndex(bars);

        // If all bars have been processed, we can return
        if (startIndex >= bars.Count)
            return;

        // Initialize with default values if starting from the beginning
        int currentDirection = startIndex > 0 ? _direction[startIndex - 1] : 1;
        double supertrendLine = startIndex > 0 ? _supertrendLine[startIndex - 1] : 0;

        // Calculate ATR and Supertrend for each bar
        for (int i = startIndex; i < bars.Count; i++)
        {
            double src = (bars[i].High + bars[i].Low) / 2; // hl2 in pine script

            // Calculate True Range
            double trueRange;
            if (i == 0)
            {
                trueRange = bars[i].High - bars[i].Low;
            }
            else
            {
                double tr1 = bars[i].High - bars[i].Low;
                double tr2 = Math.Abs(bars[i].High - bars[i - 1].Close);
                double tr3 = Math.Abs(bars[i].Low - bars[i - 1].Close);
                trueRange = Math.Max(Math.Max(tr1, tr2), tr3);
            }

            // Calculate ATR using RMA (Wilder's smoothing)
            double atrValue;
            if (i == 0)
            {
                atrValue = trueRange;
            }
            else if (i < Parameter.AtrPeriod)
            {
                // Simple average for initial bars
                double sum = 0;
                for (int j = 0; j <= i; j++)
                {
                    if (j == 0)
                        sum += _atrValues[0];
                    else
                        sum += Math.Max(Math.Max(
                            bars[j].High - bars[j].Low,
                            Math.Abs(bars[j].High - bars[j - 1].Close)),
                            Math.Abs(bars[j].Low - bars[j - 1].Close));
                }
                atrValue = sum / (i + 1);
            }
            else
            {
                // Wilder's smoothing (RMA in pine script)
                atrValue = (_atrValues[i - 1] * (Parameter.AtrPeriod - 1) + trueRange) / Parameter.AtrPeriod;
            }

            if (i >= _atrValues.Count)
                _atrValues.Add(atrValue);
            else
                _atrValues[i] = atrValue;

            // Calculate basic bands for the current bar
            double basicUpperBand = src + Parameter.Factor * atrValue;
            double basicLowerBand = src - Parameter.Factor * atrValue;

            // Calculate final upper and lower bands
            double finalUpperBand, finalLowerBand;

            if (i == 0)
            {
                finalUpperBand = basicUpperBand;
                finalLowerBand = basicLowerBand;
                supertrendLine = (bars[i].Close > basicUpperBand) ? basicLowerBand : basicUpperBand;
                currentDirection = (bars[i].Close > basicUpperBand) ? 1 : -1;
            }
            else
            {
                // Calculate Final Upper Band
                if (basicUpperBand < _upperBand[i - 1] || bars[i - 1].Close > _upperBand[i - 1])
                {
                    finalUpperBand = basicUpperBand;
                }
                else
                {
                    finalUpperBand = _upperBand[i - 1];
                }

                // Calculate Final Lower Band
                if (basicLowerBand > _lowerBand[i - 1] || bars[i - 1].Close < _lowerBand[i - 1])
                {
                    finalLowerBand = basicLowerBand;
                }
                else
                {
                    finalLowerBand = _lowerBand[i - 1];
                }

                // Determine Direction and Supertrend Line
                if (_direction[i - 1] == -1 && bars[i].Close > finalUpperBand)
                {
                    // Downtrend broken, switch to uptrend
                    currentDirection = 1;
                    supertrendLine = finalLowerBand;
                }
                else if (_direction[i - 1] == 1 && bars[i].Close < finalLowerBand)
                {
                    // Uptrend broken, switch to downtrend
                    currentDirection = -1;
                    supertrendLine = finalUpperBand;
                }
                else
                {
                    // Trend continues
                    currentDirection = _direction[i - 1];
                    supertrendLine = currentDirection == 1 ? finalLowerBand : finalUpperBand;
                }
            }

            // Update or add values to our lists
            if (i >= _upperBand.Count)
            {
                _upperBand.Add(finalUpperBand);
                _lowerBand.Add(finalLowerBand);
                _supertrendLine.Add(supertrendLine);
                _direction.Add(currentDirection);
            }
            else
            {
                _upperBand[i] = finalUpperBand;
                _lowerBand[i] = finalLowerBand;
                _supertrendLine[i] = supertrendLine;
                _direction[i] = currentDirection;
            }

            // Record this timestamp as processed using base class method
            RecordProcessed(bars[i].TimeStamp, i);
        }
    }

    // Ensure our collections can hold at least the specified capacity
    private void EnsureCapacity(int capacity)
    {
        int currentSize = _upperBand.Count;
        if (currentSize < capacity)
        {
            // No need to resize if we're starting fresh
            if (currentSize > 0)
            {
                // Only add the difference between capacity and current size
                int newItems = capacity - currentSize;
                for (int i = 0; i < newItems; i++)
                {
                    _upperBand.Add(0);
                    _lowerBand.Add(0);
                    _supertrendLine.Add(0);
                    _direction.Add(0);
                    _atrValues.Add(0);
                }
            }
        }
    }

    public TrendSignal GetTrendSignal()
    {
        if (_direction.Count == 0)
            return TrendSignal.Neutral;

        int lastDirection = _direction[^1];

        if (lastDirection == 1)
            return TrendSignal.Bullish;
        else if (lastDirection == -1)
            return TrendSignal.Bearish;
        else
            return TrendSignal.Neutral;
    }
}