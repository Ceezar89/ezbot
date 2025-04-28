using EzBot.Models;
using EzBot.Core.IndicatorParameter;

namespace EzBot.Core.Indicator;
public class AtrBands(AtrBandsParameter parameter)
    : IndicatorBase<AtrBandsParameter>(parameter), IRiskManagementIndicator
{
    private List<double> ATRValues = [];
    private List<double> UpperBand = [];
    private List<double> LowerBand = [];
    private List<double> LongTakeProfit = [];
    private List<double> ShortTakeProfit = [];

    protected override void ProcessBarData(List<BarData> bars)
    {
        int count = bars.Count;
        if (count < Parameter.Period)
            throw new ArgumentException("Not enough data to calculate ATR Bands.");

        // Check if we need to resize our storage
        if (ATRValues.Count < count)
        {
            ATRValues.AddRange(new double[count - ATRValues.Count]);
            UpperBand.AddRange(new double[count - UpperBand.Count]);
            LowerBand.AddRange(new double[count - LowerBand.Count]);
            LongTakeProfit.AddRange(new double[count - LongTakeProfit.Count]);
            ShortTakeProfit.AddRange(new double[count - ShortTakeProfit.Count]);
        }

        List<double> SrcUpper = [.. bars.Select(b => b.Close)];
        List<double> SrcLower = [.. bars.Select(b => b.Close)];
        List<double> trueRange = new(count);

        // Fill trueRange with placeholder values first
        for (int i = 0; i < count; i++)
            trueRange.Add(0);

        // Find the first bar we need to process using base class method
        int startIndex = FindStartIndex(bars);

        // If all bars have been processed, we can return
        if (startIndex >= count)
            return;

        // Calculate the True Range for all bars
        for (int i = 0; i < count; i++)
        {
            if (i < startIndex && IsProcessed(bars[i].TimeStamp))
            {
                // Skip calculation for already processed bars
                continue;
            }

            if (i == 0)
            {
                trueRange[i] = bars[i].High - bars[i].Low;
            }
            else
            {
                trueRange[i] = Math.Max(
                    bars[i].High - bars[i].Low,
                    Math.Max(
                        Math.Abs(bars[i].High - bars[i - 1].Close),
                        Math.Abs(bars[i].Low - bars[i - 1].Close)
                    )
                );
            }
        }

        // Calculate ATR values
        for (int i = 0; i < count; i++)
        {
            if (i < startIndex && IsProcessed(bars[i].TimeStamp))
            {
                // Skip calculation for already processed bars
                continue;
            }

            if (i == 0)
                ATRValues[i] = trueRange[i];
            else if (i < Parameter.Period)
            {
                double sumTR = 0.0;
                for (int j = 0; j <= i; j++)
                {
                    sumTR += trueRange[j];
                }
                ATRValues[i] = sumTR / (i + 1);
            }
            else
                ATRValues[i] = ((ATRValues[i - 1] * (Parameter.Period - 1)) + trueRange[i]) / Parameter.Period;

            UpperBand[i] = SrcUpper[i] + ATRValues[i] * Parameter.Multiplier;
            LowerBand[i] = SrcLower[i] - ATRValues[i] * Parameter.Multiplier;

            // Calculate take profit based on risk-reward ratio using the current bar's price
            double currentBarPrice = bars[i].Close;

            // For long positions, distance to take profit = distance to stop * risk-reward ratio
            double longStopDistance = currentBarPrice - LowerBand[i];
            LongTakeProfit[i] = currentBarPrice + (longStopDistance * Parameter.RiskRewardRatio);

            // For short positions, distance to take profit = distance to stop * risk-reward ratio
            double shortStopDistance = UpperBand[i] - currentBarPrice;
            ShortTakeProfit[i] = currentBarPrice - (shortStopDistance * Parameter.RiskRewardRatio);

            // Record this timestamp as processed using base class method
            RecordProcessed(bars[i].TimeStamp, i);
        }
    }

    public double GetLongStopLoss() => LowerBand.Count > 0 ? LowerBand.Last() : 0;

    public double GetShortStopLoss() => UpperBand.Count > 0 ? UpperBand.Last() : 0;

    public double GetLongTakeProfit() => LongTakeProfit.Count > 0 ? LongTakeProfit.Last() : 0;

    public double GetShortTakeProfit() => ShortTakeProfit.Count > 0 ? ShortTakeProfit.Last() : 0;
}