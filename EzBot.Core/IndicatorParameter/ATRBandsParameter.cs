
namespace EzBot.Core.IndicatorParameter;

public class AtrBandsParameter : IIndicatorParameter
{
    public string Name { get; set; } = "atr_bands";
    public int Period { get; set; } = 14;
    public double MultiplierUpper { get; set; } = 2.0;
    public double MultiplierLower { get; set; } = 2.0;
    public double RiskRewardRatio { get; set; } = 1.1;

    // Ranges
    private (int Min, int Max) PeriodRange = (6, 28);
    private (double Min, double Max) MultiplierUpperRange = (0.5, 5.0);
    private (double Min, double Max) MultiplierLowerRange = (0.5, 5.0);
    private (double Min, double Max) RiskRewardRatioRange = (1.1, 2.0);

    // Steps
    private const int PeriodRangeStep = 2;
    private const double MultiplierUpperRangeStep = 0.5;
    private const double MultiplierLowerRangeStep = 0.5;
    private const double RiskRewardRatioRangeStep = 0.1;

    public AtrBandsParameter()
    {
        Name = "atr_bands";
        Period = PeriodRange.Min;
        MultiplierUpper = MultiplierUpperRange.Min;
        MultiplierLower = MultiplierLowerRange.Min;
        RiskRewardRatio = RiskRewardRatioRange.Min;
    }

    public AtrBandsParameter(string name, int period, double multiplierUpper, double multiplierLower, double riskRewardRatio)
    {
        Name = name;
        Period = period;
        MultiplierUpper = multiplierUpper;
        MultiplierLower = multiplierLower;
        RiskRewardRatio = riskRewardRatio;
    }

    public void IncrementSingle()
    {
        if (Period < PeriodRange.Max)
        {
            Period += PeriodRangeStep;
        }
        else if (MultiplierUpper < MultiplierUpperRange.Max)
        {
            MultiplierUpper += MultiplierUpperRangeStep;
        }
        else if (MultiplierLower < MultiplierLowerRange.Max)
        {
            MultiplierLower += MultiplierLowerRangeStep;
        }
        else if (RiskRewardRatio < RiskRewardRatioRange.Max)
        {
            RiskRewardRatio += RiskRewardRatioRangeStep;
        }
    }

    public bool CanIncrement()
    {
        return Period < PeriodRange.Max
                || MultiplierUpper < MultiplierUpperRange.Max
                || MultiplierLower < MultiplierLowerRange.Max
                || RiskRewardRatio < RiskRewardRatioRange.Max;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Period, MultiplierUpper, MultiplierLower, RiskRewardRatio);
    }

    public bool Equals(IIndicatorParameter? other)
    {
        if (other == null || GetType() != other.GetType())
        {
            return false;
        }

        var p = (AtrBandsParameter)other;
        return (Name == p.Name)
                && (Period == p.Period)
                && (MultiplierUpper == p.MultiplierUpper)
                && (MultiplierLower == p.MultiplierLower)
                && (RiskRewardRatio == p.RiskRewardRatio);
    }
}
