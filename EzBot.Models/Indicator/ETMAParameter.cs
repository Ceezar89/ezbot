namespace EzBot.Models.Indicator;

public class EtmaParameter : IIndicatorParameter
{
    public string Name { get; set; } = "etma";
    public int Lenght { get; set; } = 14;
    public SignalStrength SignalStrength { get; set; } = SignalStrength.VeryStrong;

    // Ranges
    public static (int Min, int Max) LenghtRange => (1, 25);
    public static (int Min, int Max) SignalStrengthRange => (0, 2);

    // Steps
    public const int LenghtRangeStep = 1;
    public const int SignalStrengthRangeStep = 1;

    public EtmaParameter()
    {
    }

    public EtmaParameter(string name, int length, SignalStrength signalStrength)
    {
        Name = name;
        Lenght = length;
        SignalStrength = signalStrength;
    }
}
