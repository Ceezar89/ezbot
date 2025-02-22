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
