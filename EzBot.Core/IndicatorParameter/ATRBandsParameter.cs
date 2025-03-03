namespace EzBot.Core.IndicatorParameter;

public class AtrBandsParameter : IndicatorParameterBase
{
    private int _period = 14;
    private double _multiplierUpper = 2.0;
    private double _multiplierLower = 2.0;
    private double _riskRewardRatio = 1.1;

    // Ranges
    private static readonly (int Min, int Max) PeriodRange = (6, 28);
    private static readonly (double Min, double Max) MultiplierUpperRange = (0.5, 5.0);
    private static readonly (double Min, double Max) MultiplierLowerRange = (0.5, 5.0);
    private static readonly (double Min, double Max) RiskRewardRatioRange = (1.1, 2.0);

    // Steps
    private const int PeriodRangeStep = 2;
    private const double MultiplierUpperRangeStep = 0.5;
    private const double MultiplierLowerRangeStep = 0.5;
    private const double RiskRewardRatioRangeStep = 0.1;

    public override List<ParameterDescriptor> GetProperties()
    {
        return [
            new ParameterDescriptor(_period, PeriodRange.Min, PeriodRange.Max, PeriodRangeStep, "Period"),
            new ParameterDescriptor(_multiplierUpper, MultiplierUpperRange.Min, MultiplierUpperRange.Max, MultiplierUpperRangeStep, "Multiplier Upper"),
            new ParameterDescriptor(_multiplierLower, MultiplierLowerRange.Min, MultiplierLowerRange.Max, MultiplierLowerRangeStep, "Multiplier Lower"),
            new ParameterDescriptor(_riskRewardRatio, RiskRewardRatioRange.Min, RiskRewardRatioRange.Max, RiskRewardRatioRangeStep, "Risk Reward Ratio")
        ];
    }

    public override void UpdateFromDescriptor(ParameterDescriptor descriptor)
    {
        switch (descriptor.Name)
        {
            case "Period":
                Period = (int)descriptor.Value;
                break;
            case "Multiplier Upper":
                MultiplierUpper = (double)descriptor.Value;
                break;
            case "Multiplier Lower":
                MultiplierLower = (double)descriptor.Value;
                break;
            case "Risk Reward Ratio":
                RiskRewardRatio = (double)descriptor.Value;
                break;
        }
    }


    public int Period
    {
        get => _period;
        set => ValidateAndSetValue(ref _period, value, PeriodRange);
    }

    public double MultiplierUpper
    {
        get => _multiplierUpper;
        set => ValidateAndSetValue(ref _multiplierUpper, value, MultiplierUpperRange);
    }

    public double MultiplierLower
    {
        get => _multiplierLower;
        set => ValidateAndSetValue(ref _multiplierLower, value, MultiplierLowerRange);
    }

    public double RiskRewardRatio
    {
        get => _riskRewardRatio;
        set => ValidateAndSetValue(ref _riskRewardRatio, value, RiskRewardRatioRange);
    }

    public AtrBandsParameter() : base("atr_bands")
    {
        Period = PeriodRange.Min;
        MultiplierUpper = MultiplierUpperRange.Min;
        MultiplierLower = MultiplierLowerRange.Min;
        RiskRewardRatio = RiskRewardRatioRange.Min;
    }

    public AtrBandsParameter(string name, int period, double multiplierUpper, double multiplierLower, double riskRewardRatio)
        : base(name)
    {
        Period = period;
        MultiplierUpper = multiplierUpper;
        MultiplierLower = multiplierLower;
        RiskRewardRatio = riskRewardRatio;
    }

    public override void IncrementSingle()
    {
        if (IncrementValue(ref _period, PeriodRangeStep, PeriodRange))
            return;
        if (IncrementValue(ref _multiplierUpper, MultiplierUpperRangeStep, MultiplierUpperRange))
            return;
        if (IncrementValue(ref _multiplierLower, MultiplierLowerRangeStep, MultiplierLowerRange))
            return;
        IncrementValue(ref _riskRewardRatio, RiskRewardRatioRangeStep, RiskRewardRatioRange);
    }

    public override bool CanIncrement()
    {
        return Period < PeriodRange.Max
                || MultiplierUpper < MultiplierUpperRange.Max
                || MultiplierLower < MultiplierLowerRange.Max
                || RiskRewardRatio < RiskRewardRatioRange.Max;
    }

    public override AtrBandsParameter DeepClone()
    {
        return new AtrBandsParameter(Name, Period, MultiplierUpper, MultiplierLower, RiskRewardRatio);
    }

    public override AtrBandsParameter GetRandomNeighbor(Random random)
    {
        // Calculate how many steps are possible in each range
        int periodSteps = (PeriodRange.Max - PeriodRange.Min) / PeriodRangeStep + 1;
        int multiplierUpperSteps = (int)Math.Floor((MultiplierUpperRange.Max - MultiplierUpperRange.Min) / MultiplierUpperRangeStep) + 1;
        int multiplierLowerSteps = (int)Math.Floor((MultiplierLowerRange.Max - MultiplierLowerRange.Min) / MultiplierLowerRangeStep) + 1;
        int riskRewardRatioSteps = (int)Math.Floor((RiskRewardRatioRange.Max - RiskRewardRatioRange.Min) / RiskRewardRatioRangeStep) + 1;

        // Choose a random step for each parameter
        var period = PeriodRange.Min + (random.Next(periodSteps) * PeriodRangeStep);
        var multiplierUpper = MultiplierUpperRange.Min + (random.Next(multiplierUpperSteps) * MultiplierUpperRangeStep);
        var multiplierLower = MultiplierLowerRange.Min + (random.Next(multiplierLowerSteps) * MultiplierLowerRangeStep);
        var riskRewardRatio = RiskRewardRatioRange.Min + (random.Next(riskRewardRatioSteps) * RiskRewardRatioRangeStep);

        return new AtrBandsParameter(Name, period, multiplierUpper, multiplierLower, riskRewardRatio);
    }

    protected override int GetAdditionalHashCodeComponents()
    {
        return HashCode.Combine(Period, MultiplierUpper, MultiplierLower, RiskRewardRatio);
    }

    protected override bool EqualsCore(IIndicatorParameter other)
    {
        var p = (AtrBandsParameter)other;
        return Period == p.Period
                && MultiplierUpper == p.MultiplierUpper
                && MultiplierLower == p.MultiplierLower
                && RiskRewardRatio == p.RiskRewardRatio;
    }
}
