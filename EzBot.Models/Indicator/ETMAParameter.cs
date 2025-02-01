namespace EzBot.Models.Indicator;

public class EtmaParameter : IIndicatorParameter
{
    public string Id { get; set; }
    public int Lenght { get; set; } = 14;
    public SignalStrength SignalStrength { get; set; } = SignalStrength.VeryStrong;

    public EtmaParameter(string id, int length, SignalStrength signalStrength)
    {
        Id = id;
        Lenght = length;
        SignalStrength = signalStrength;
    }

    public EtmaParameter(string id)
    {
        Id = id;
    }

}
