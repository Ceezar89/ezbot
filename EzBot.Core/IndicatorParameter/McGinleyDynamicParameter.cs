using EzBot.Models;

namespace EzBot.Core.IndicatorParameter;

public class McGinleyDynamicParameter : IndicatorParameterBase
{
    private int _period = 14;

    // Ranges
    private static readonly (int Min, int Max) PeriodRange = (10, 30);

    // Steps
    private const int PeriodRangeStep = 2;

    // Calculate permutations
    private static readonly int PeriodPermutations = CalculateSteps(PeriodRange.Min, PeriodRange.Max, PeriodRangeStep);

    public static readonly int TotalPermutations = PeriodPermutations;

    public override int GetPermutationCount() => TotalPermutations;

    public override List<ParameterDescriptor> GetProperties()
    {
        return [
            new ParameterDescriptor(_period, PeriodRange.Min, PeriodRange.Max, PeriodRangeStep, "Period")
        ];
    }

    public override void UpdateFromDescriptor(ParameterDescriptor descriptor)
    {
        switch (descriptor.Name)
        {
            case "Period":
                Period = (int)descriptor.Value;
                break;
        }
    }

    public int Period
    {
        get => _period;
        set => ValidateAndSetValue(ref _period, value, PeriodRange);
    }

    public McGinleyDynamicParameter() : base("mcginley_dynamic")
    {
        Period = PeriodRange.Min;
    }

    public McGinleyDynamicParameter(int period) : base("mcginley_dynamic")
    {
        Period = period;
    }

    public override void IncrementSingle()
    {
        if (_period + PeriodRangeStep <= PeriodRange.Max)
        {
            _period += PeriodRangeStep;
        }
    }

    public override McGinleyDynamicParameter DeepClone()
    {
        return new McGinleyDynamicParameter(Period);
    }

    public override McGinleyDynamicParameter GetRandomNeighbor(Random random)
    {
        // Calculate how many steps are possible in the range
        int periodSteps = (PeriodRange.Max - PeriodRange.Min) / PeriodRangeStep + 1;

        // Choose a random step
        var period = PeriodRange.Min + (random.Next(periodSteps) * PeriodRangeStep);

        return new McGinleyDynamicParameter(period);
    }

    public override bool CanIncrement()
    {
        return _period + PeriodRangeStep <= PeriodRange.Max;
    }

    public override void Reset()
    {
        Period = PeriodRange.Min;
    }

    protected override int GetAdditionalHashCodeComponents()
    {
        return Period.GetHashCode();
    }

    protected override bool EqualsCore(IIndicatorParameter other)
    {
        var p = (McGinleyDynamicParameter)other;
        return Period == p.Period;
    }
}