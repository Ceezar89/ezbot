using EzBot.Models;
using EzBot.Core.IndicatorParameter;

namespace EzBot.Core.Indicator;
public class AtrBands(AtrBandsParameter parameter) : IndicatorBase<AtrBandsParameter>(parameter), IRiskManagementIndicator
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

        ATRValues = [.. new double[count]];
        UpperBand = [.. new double[count]];
        LowerBand = [.. new double[count]];
        LongTakeProfit = [.. new double[count]];
        ShortTakeProfit = [.. new double[count]];

        List<double> SrcUpper = [.. bars.Select(b => b.Close)];
        List<double> SrcLower = [.. bars.Select(b => b.Close)];
        List<double> trueRange = [];

        for (int i = 0; i < count; i++)
        {
            if (i == 0)
            {
                double tr = bars[i].High - bars[i].Low;
                trueRange.Add(tr);
            }
            else
            {
                double tr = Math.Max(
                    bars[i].High - bars[i].Low,
                    Math.Max(
                        Math.Abs(bars[i].High - bars[i - 1].Close),
                        Math.Abs(bars[i].Low - bars[i - 1].Close)
                    )
                );
                trueRange.Add(tr);
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
        }
    }

    public double GetLongStopLoss() => LowerBand.Last();

    public double GetShortStopLoss() => UpperBand.Last();

    public double GetLongTakeProfit() => LongTakeProfit.Last();

    public double GetShortTakeProfit() => ShortTakeProfit.Last();
}