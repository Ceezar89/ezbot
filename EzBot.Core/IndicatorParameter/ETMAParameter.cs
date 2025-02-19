using EzBot.Models;

namespace EzBot.Core.IndicatorParameter;

public class EtmaParameter : IIndicatorParameter
{
    public string Name { get; set; } = "etma";
    public int Lenght { get; set; } = 14;
    public SignalStrength SignalStrength { get; set; } = SignalStrength.VeryStrong;

    // Ranges
    private (int Min, int Max) LenghtRange = (1, 25);
    private (int Min, int Max) SignalStrengthRange = (0, 2);

    // Steps
    private const int LenghtRangeStep = 1;
    private const int SignalStrengthRangeStep = 1;

    public EtmaParameter()
    {
        Name = "etma";
        Lenght = LenghtRange.Min;
        SignalStrength = (SignalStrength)SignalStrengthRange.Min;
    }

    public EtmaParameter(int length, SignalStrength signalStrength)
    {
        Lenght = length;
        SignalStrength = signalStrength;
    }

    public void IncrementSingle()
    {
        if (Lenght < LenghtRange.Max)
        {
            Lenght += LenghtRangeStep;
        }
        else if ((int)SignalStrength < SignalStrengthRange.Max)
        {
            SignalStrength += SignalStrengthRangeStep;
        }
    }

    public bool CanIncrement()
    {
        return Lenght < LenghtRange.Max || (int)SignalStrength < SignalStrengthRange.Max;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Lenght, SignalStrength);
    }

    public bool Equals(IIndicatorParameter? other)
    {
        if (other == null || GetType() != other.GetType())
        {
            return false;
        }

        var p = (EtmaParameter)other;
        return (Name == p.Name) && (Lenght == p.Lenght) && (SignalStrength == p.SignalStrength);
    }
}
