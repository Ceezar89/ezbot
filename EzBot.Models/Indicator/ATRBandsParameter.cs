namespace EzBot.Models.Indicator;

public class AtrBandsParameter : IIndicatorParameter
{
    public string Id { get; set; }
    public int Period { get; set; } = 14;
    public double MultiplierUpper { get; set; } = 2.0;
    public double MultiplierLower { get; set; } = 2.0;

    public AtrBandsParameter(string id, int period, double multiplierUpper, double multiplierLower)
    {
        Id = id;
        Period = period;
        MultiplierUpper = multiplierUpper;
        MultiplierLower = multiplierLower;
    }

    public AtrBandsParameter(string id)
    {
        Id = id;
    }
}
