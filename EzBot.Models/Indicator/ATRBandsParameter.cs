namespace EzBot.Models.Indicator;

public class AtrBandsParameter : IIndicatorParameter
{
    public string Name { get; set; } = "atr_bands";
    public int Period { get; set; } = 14;
    public double MultiplierUpper { get; set; } = 2.0;
    public double MultiplierLower { get; set; } = 2.0;

    // Ranges
    public static (int Min, int Max) PeriodRange => (6, 28);
    public static (double Min, double Max) MultiplierUpperRange => (0.5, 5.0);
    public static (double Min, double Max) MultiplierLowerRange => (0.5, 5.0);

    // Steps
    public const int PeriodRangeStep = 2;
    public const double MultiplierUpperRangeStep = 0.5;
    public const double MultiplierLowerRangeStep = 0.5;

    public AtrBandsParameter()
    {
    }

    public AtrBandsParameter(string name, int period, double multiplierUpper, double multiplierLower)
    {
        Name = name;
        Period = period;
        MultiplierUpper = multiplierUpper;
        MultiplierLower = multiplierLower;
    }
}
