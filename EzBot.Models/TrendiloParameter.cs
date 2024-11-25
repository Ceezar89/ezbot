namespace EzBot.Models;

public class TrendiloParameter : IndicatorParameter
{

    public int SmoothTrending { get; set; }
    public int Lookback { get; set; }
    public double AlmaOffsetTrend { get; set; }
    public int AlmaSigma { get; set; }
    public double BandMultiplier { get; set; }

    public TrendiloParameter(string id, int smoothTrending, int lookback, double almaOffsetTrend, int almaSigma, double bandMultiplier)
    {
        Id = id;
        SmoothTrending = smoothTrending;
        Lookback = lookback;
        AlmaOffsetTrend = almaOffsetTrend;
        AlmaSigma = almaSigma;
        BandMultiplier = bandMultiplier;
    }

}
