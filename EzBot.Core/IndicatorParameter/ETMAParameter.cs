using EzBot.Models;

namespace EzBot.Core.IndicatorParameter;

public class EtmaParameter : IndicatorParameterBase
{
    private int _length = 14;
    private SignalStrength _signalStrength = SignalStrength.VeryStrong;

    // Ranges
    private static readonly (int Min, int Max) LengthRange = (12, 16);
    private static readonly (int Min, int Max) SignalStrengthRange = (0, 2);

    // Steps
    private const int LengthRangeStep = 2;
    private const int SignalStrengthRangeStep = 1;

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
        if (IncrementValue(ref _length, LengthRangeStep, LengthRange))
            return;

        int signalStrengthInt = (int)_signalStrength;
        if (IncrementValue(ref signalStrengthInt, SignalStrengthRangeStep, SignalStrengthRange))
        {
            _signalStrength = (SignalStrength)signalStrengthInt;
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
        return Length < LengthRange.Max || (int)SignalStrength < SignalStrengthRange.Max;
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
