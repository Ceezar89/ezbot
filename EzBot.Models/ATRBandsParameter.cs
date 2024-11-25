namespace EzBot.Models;

public class ATRBandsParameter : Parameter
{
    public int Period { get; set; }
    public double Multiplier { get; set; }

    public ATRBandsParameter(string id, int period, double multiplier)
    {
        Id = id;
        Period = period;
        Multiplier = multiplier;
    }
}
