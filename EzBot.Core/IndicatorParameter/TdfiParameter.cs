using EzBot.Models;

namespace EzBot.Core.IndicatorParameter;

public class TdfiParameter : IndicatorParameterBase
{
    private int _lookback = 13;

    // Ranges
    private static readonly (int Min, int Max) LookbackRange = (6, 20);

    // Steps
    private const int LookbackRangeStep = 1;

    // Calculate permutations
    private static readonly int LookbackPermutations = CalculateSteps(LookbackRange.Min, LookbackRange.Max, LookbackRangeStep);

    public static readonly int TotalPermutations = LookbackPermutations;

    public override int GetPermutationCount() => TotalPermutations;

    public override List<ParameterDescriptor> GetProperties()
    {
        return [
            new ParameterDescriptor(_lookback, LookbackRange.Min, LookbackRange.Max, LookbackRangeStep, "Lookback")
        ];
    }

    public override void UpdateFromDescriptor(ParameterDescriptor descriptor)
    {
        switch (descriptor.Name)
        {
            case "Lookback":
                Lookback = (int)descriptor.Value;
                break;
        }
    }

    public int Lookback
    {
        get => _lookback;
        set => ValidateAndSetValue(ref _lookback, value, LookbackRange);
    }

    // Constants that are not changeable
    public int MmaLength { get { return 13; } }
    public int SmmaLength { get { return 13; } }
    public int NLength { get { return 3; } }
    public double FilterHigh { get { return 0.05; } }
    public double FilterLow { get { return -0.05; } }
    public string MmaMode { get { return "ema"; } }
    public string SmmaMode { get { return "ema"; } }
    public bool UseCrossConfirmation { get { return true; } }
    public bool UseInverse { get { return true; } }

    public TdfiParameter() : base("tdfi")
    {
        Lookback = LookbackRange.Min;
    }

    public TdfiParameter(int lookback) : base("tdfi")
    {
        Lookback = lookback;
    }

    public override void IncrementSingle()
    {
        if (_lookback + LookbackRangeStep <= LookbackRange.Max)
        {
            _lookback += LookbackRangeStep;
        }
    }

    public override TdfiParameter DeepClone()
    {
        return new TdfiParameter(Lookback);
    }

    public override TdfiParameter GetRandomNeighbor(Random random)
    {
        // Calculate how many steps are possible in the range
        int lookbackSteps = (LookbackRange.Max - LookbackRange.Min) / LookbackRangeStep + 1;

        // Choose a random step
        var lookback = LookbackRange.Min + (random.Next(lookbackSteps) * LookbackRangeStep);

        return new TdfiParameter(lookback);
    }

    public override bool CanIncrement()
    {
        return _lookback + LookbackRangeStep <= LookbackRange.Max;
    }

    public override void Reset()
    {
        Lookback = LookbackRange.Min;
    }

    protected override int GetAdditionalHashCodeComponents()
    {
        return Lookback.GetHashCode();
    }

    protected override bool EqualsCore(IIndicatorParameter other)
    {
        var p = (TdfiParameter)other;
        return Lookback == p.Lookback;
    }
}