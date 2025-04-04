namespace EzBot.Core.IndicatorParameter;

public class AtrBandsParameter : IndicatorParameterBase
{
    private int _period = 14;
    private double _multiplier = 2.0;
    private double _riskRewardRatio = 1.1;

    // Ranges
    private static readonly (int Min, int Max) PeriodRange = (10, 16);
    private static readonly (double Min, double Max) MultiplierRange = (2.0, 4.0);
    private static readonly (double Min, double Max) RiskRewardRatioRange = (1.1, 1.5);

    // Steps
    private const int PeriodRangeStep = 2;
    private const double MultiplierRangeStep = 1.0;
    private const double RiskRewardRatioRangeStep = 0.1;

    public override List<ParameterDescriptor> GetProperties()
    {
        return [
            new ParameterDescriptor(_period, PeriodRange.Min, PeriodRange.Max, PeriodRangeStep, "Period"),
            new ParameterDescriptor(_multiplier, MultiplierRange.Min, MultiplierRange.Max, MultiplierRangeStep, "Multiplier Upper"),
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
            case "Multiplier":
                Multiplier = (double)descriptor.Value;
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

    public double Multiplier
    {
        get => _multiplier;
        set => ValidateAndSetValue(ref _multiplier, value, MultiplierRange);
    }

    public double RiskRewardRatio
    {
        get => _riskRewardRatio;
        set => ValidateAndSetValue(ref _riskRewardRatio, value, RiskRewardRatioRange);
    }

    public AtrBandsParameter() : base("atr_bands")
    {
        Period = PeriodRange.Min;
        Multiplier = MultiplierRange.Min;
        RiskRewardRatio = RiskRewardRatioRange.Min;
    }

    public AtrBandsParameter(string name, int period, double multiplier, double riskRewardRatio)
        : base(name)
    {
        Period = period;
        Multiplier = multiplier;
        RiskRewardRatio = riskRewardRatio;
    }

    public override void IncrementSingle()
    {
        if (IncrementValue(ref _period, PeriodRangeStep, PeriodRange))
            return;
        if (IncrementValue(ref _multiplier, MultiplierRangeStep, MultiplierRange))
            return;
        IncrementValue(ref _riskRewardRatio, RiskRewardRatioRangeStep, RiskRewardRatioRange);
    }

    public override bool CanIncrement()
    {
        return Period < PeriodRange.Max
                || Multiplier < MultiplierRange.Max
                || RiskRewardRatio < RiskRewardRatioRange.Max;
    }

    public override AtrBandsParameter DeepClone()
    {
        return new AtrBandsParameter(Name, Period, Multiplier, RiskRewardRatio);
    }

    public override AtrBandsParameter GetRandomNeighbor(Random random)
    {
        // Calculate how many steps are possible in each range
        int periodSteps = (PeriodRange.Max - PeriodRange.Min) / PeriodRangeStep + 1;
        int multiplierSteps = (int)Math.Floor((MultiplierRange.Max - MultiplierRange.Min) / MultiplierRangeStep) + 1;
        int riskRewardRatioSteps = (int)Math.Floor((RiskRewardRatioRange.Max - RiskRewardRatioRange.Min) / RiskRewardRatioRangeStep) + 1;

        // Choose a random step for each parameter
        var period = PeriodRange.Min + (random.Next(periodSteps) * PeriodRangeStep);
        var multiplier = MultiplierRange.Min + (random.Next(multiplierSteps) * MultiplierRangeStep);
        var riskRewardRatio = RiskRewardRatioRange.Min + (random.Next(riskRewardRatioSteps) * RiskRewardRatioRangeStep);

        return new AtrBandsParameter(Name, period, multiplier, riskRewardRatio);
    }

    protected override int GetAdditionalHashCodeComponents()
    {
        return HashCode.Combine(Period, Multiplier, RiskRewardRatio);
    }

    protected override bool EqualsCore(IIndicatorParameter other)
    {
        var p = (AtrBandsParameter)other;
        return Period == p.Period
                && Multiplier == p.Multiplier
                && RiskRewardRatio == p.RiskRewardRatio;
    }
}
