using EzBot.Models;

namespace EzBot.Core.IndicatorParameter;

public class EtmaParameter : IndicatorParameterBase
{
    private int _length = 14;
    private SignalStrength _signalStrength = SignalStrength.VeryStrong;

    // Ranges
    private static readonly (int Min, int Max) LengthRange = (1, 25);
    private static readonly (int Min, int Max) SignalStrengthRange = (0, 2);

    // Steps
    private const int LengthRangeStep = 1;
    private const int SignalStrengthRangeStep = 1;

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
