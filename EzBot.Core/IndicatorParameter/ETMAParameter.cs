using EzBot.Models;

namespace EzBot.Core.IndicatorParameter;

public class EtmaParameter : IndicatorParameterBase
{
    private int _length = 14;
    private SignalStrength _signalStrength = SignalStrength.VeryStrong;

    // Ranges
    private static readonly (int Min, int Max) LengthRange = (20, 22);
    private static readonly (int Min, int Max) SignalStrengthRange = (0, 2);

    // Steps
    private const int LengthRangeStep = 1;
    private const int SignalStrengthRangeStep = 1;

    // Correctly calculate the number of steps for each parameter range
    private static readonly int LengthPermutations = CalculateSteps(LengthRange.Min, LengthRange.Max, LengthRangeStep);
    private static readonly int SignalStrengthPermutations = CalculateSteps(SignalStrengthRange.Min, SignalStrengthRange.Max, SignalStrengthRangeStep);

    public static readonly int TotalPermutations = LengthPermutations * SignalStrengthPermutations;

    public override int GetPermutationCount() => TotalPermutations;

    public override List<ParameterDescriptor> GetProperties()
    {
        return [
            new ParameterDescriptor(_length, LengthRange.Min, LengthRange.Max, LengthRangeStep, "Length"),
            new ParameterDescriptor((int)_signalStrength, SignalStrengthRange.Min, SignalStrengthRange.Max, SignalStrengthRangeStep, "Signal Strength")
        ];
    }

    public override void UpdateFromDescriptor(ParameterDescriptor descriptor)
    {
        switch (descriptor.Name)
        {
            case "Length":
                Length = (int)descriptor.Value;
                break;
            case "Signal Strength":
                SignalStrength = (SignalStrength)(int)descriptor.Value;
                break;
        }
    }

    public int Length
    {
        get => _length;
        set => ValidateAndSetValue(ref _length, value, LengthRange);
    }

    public SignalStrength SignalStrength
    {
        get => _signalStrength;
        set
        {
            int intValue = (int)value;
            ValidateAndSetValue(ref intValue, intValue, SignalStrengthRange);
            _signalStrength = (SignalStrength)intValue;
        }
    }

    public EtmaParameter() : base("etma")
    {
        Length = LengthRange.Min;
        SignalStrength = (SignalStrength)SignalStrengthRange.Min;
    }

    public EtmaParameter(int length, SignalStrength signalStrength) : base("etma")
    {
        Length = length;
        SignalStrength = signalStrength;
    }

    public override void IncrementSingle()
    {
        // Increment parameters one by one, starting from the least significant
        // When a parameter is successfully incremented, return immediately

        // Start with the least significant parameter (Signal Strength)
        int signalStrengthInt = (int)_signalStrength;
        if (signalStrengthInt + SignalStrengthRangeStep <= SignalStrengthRange.Max)
        {
            signalStrengthInt += SignalStrengthRangeStep;
            _signalStrength = (SignalStrength)signalStrengthInt;
            return;
        }

        // Reset Signal Strength and try to increment Length
        _signalStrength = (SignalStrength)SignalStrengthRange.Min;
        if (_length + LengthRangeStep <= LengthRange.Max)
        {
            _length += LengthRangeStep;
        }
    }

    public override EtmaParameter DeepClone()
    {
        return new EtmaParameter(Length, SignalStrength);
    }

    public override EtmaParameter GetRandomNeighbor(Random random)
    {
        // Calculate how many steps are possible in each range
        int lengthSteps = (LengthRange.Max - LengthRange.Min) / LengthRangeStep + 1;
        int signalStrengthSteps = (SignalStrengthRange.Max - SignalStrengthRange.Min) / SignalStrengthRangeStep + 1;

        // Choose a random step for each parameter
        var length = LengthRange.Min + (random.Next(lengthSteps) * LengthRangeStep);
        var signalStrength = (SignalStrength)(SignalStrengthRange.Min + (random.Next(signalStrengthSteps) * SignalStrengthRangeStep));

        return new EtmaParameter(length, signalStrength);
    }

    public override bool CanIncrement()
    {
        // Check if any parameter can still be incremented
        int signalStrengthInt = (int)_signalStrength;
        if (signalStrengthInt + SignalStrengthRangeStep <= SignalStrengthRange.Max)
            return true;

        if (_length + LengthRangeStep <= LengthRange.Max)
            return true;

        return false;
    }

    public override void Reset()
    {
        Length = LengthRange.Min;
        SignalStrength = (SignalStrength)SignalStrengthRange.Min;
    }

    protected override int GetAdditionalHashCodeComponents()
    {
        return HashCode.Combine(Length, SignalStrength);
    }

    protected override bool EqualsCore(IIndicatorParameter other)
    {
        var p = (EtmaParameter)other;
        return Length == p.Length && SignalStrength == p.SignalStrength;
    }
}
