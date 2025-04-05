using EzBot.Models;

namespace EzBot.Core.IndicatorParameter;

public class LwpiParameter : IndicatorParameterBase
{
    private int _period = 8;
    private int _smoothingPeriod = 6;

    // Ranges
    private static readonly (int Min, int Max) PeriodRange = (4, 20);
    private static readonly (int Min, int Max) SmoothingPeriodRange = (2, 20);

    // Steps
    private const int PeriodRangeStep = 2;
    private const int SmoothingPeriodRangeStep = 2;

    // Calculate permutations
    private static readonly int PeriodPermutations = CalculateSteps(PeriodRange.Min, PeriodRange.Max, PeriodRangeStep);
    private static readonly int SmoothingPeriodPermutations = CalculateSteps(SmoothingPeriodRange.Min, SmoothingPeriodRange.Max, SmoothingPeriodRangeStep);

    public static readonly int TotalPermutations = PeriodPermutations * SmoothingPeriodPermutations;

    public override int GetPermutationCount() => TotalPermutations;

    public override List<ParameterDescriptor> GetProperties()
    {
        return [
            new ParameterDescriptor(_period, PeriodRange.Min, PeriodRange.Max, PeriodRangeStep, "Period"),
            new ParameterDescriptor(_smoothingPeriod, SmoothingPeriodRange.Min, SmoothingPeriodRange.Max, SmoothingPeriodRangeStep, "Smoothing Period")
        ];
    }

    public override void UpdateFromDescriptor(ParameterDescriptor descriptor)
    {
        switch (descriptor.Name)
        {
            case "Period":
                Period = (int)descriptor.Value;
                break;
            case "Smoothing Period":
                SmoothingPeriod = (int)descriptor.Value;
                break;
        }
    }

    public int Period
    {
        get => _period;
        set => ValidateAndSetValue(ref _period, value, PeriodRange);
    }

    public int SmoothingPeriod
    {
        get => _smoothingPeriod;
        set => ValidateAndSetValue(ref _smoothingPeriod, value, SmoothingPeriodRange);
    }

    // Fixed value - not changeable as per requirements
    public string SmoothingType => "SMA";

    public LwpiParameter() : base("lwpi")
    {
        Period = PeriodRange.Min;
        SmoothingPeriod = SmoothingPeriodRange.Min;
    }

    public LwpiParameter(int period, int smoothingPeriod) : base("lwpi")
    {
        Period = period;
        SmoothingPeriod = smoothingPeriod;
    }

    public override void IncrementSingle()
    {
        // Increment parameters one by one, starting from the least significant
        // When a parameter is successfully incremented, return immediately

        // Start with the least significant parameter (Smoothing Period)
        if (_smoothingPeriod + SmoothingPeriodRangeStep <= SmoothingPeriodRange.Max)
        {
            _smoothingPeriod += SmoothingPeriodRangeStep;
            return;
        }

        // Reset Smoothing Period and try to increment Period
        _smoothingPeriod = SmoothingPeriodRange.Min;
        if (_period + PeriodRangeStep <= PeriodRange.Max)
        {
            _period += PeriodRangeStep;
        }
    }

    public override LwpiParameter DeepClone()
    {
        return new LwpiParameter(Period, SmoothingPeriod);
    }

    public override LwpiParameter GetRandomNeighbor(Random random)
    {
        // Calculate how many steps are possible in each range
        int periodSteps = (PeriodRange.Max - PeriodRange.Min) / PeriodRangeStep + 1;
        int smoothingPeriodSteps = (SmoothingPeriodRange.Max - SmoothingPeriodRange.Min) / SmoothingPeriodRangeStep + 1;

        // Choose random steps
        var period = PeriodRange.Min + (random.Next(periodSteps) * PeriodRangeStep);
        var smoothingPeriod = SmoothingPeriodRange.Min + (random.Next(smoothingPeriodSteps) * SmoothingPeriodRangeStep);

        return new LwpiParameter(period, smoothingPeriod);
    }

    public override bool CanIncrement()
    {
        // Check if any parameter can still be incremented
        if (_smoothingPeriod + SmoothingPeriodRangeStep <= SmoothingPeriodRange.Max)
            return true;

        if (_period + PeriodRangeStep <= PeriodRange.Max)
            return true;

        return false;
    }

    public override void Reset()
    {
        Period = PeriodRange.Min;
        SmoothingPeriod = SmoothingPeriodRange.Min;
    }

    protected override int GetAdditionalHashCodeComponents()
    {
        return HashCode.Combine(Period, SmoothingPeriod);
    }

    protected override bool EqualsCore(IIndicatorParameter other)
    {
        var p = (LwpiParameter)other;
        return Period == p.Period && SmoothingPeriod == p.SmoothingPeriod;
    }
}