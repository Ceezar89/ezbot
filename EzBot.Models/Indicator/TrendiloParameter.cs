namespace EzBot.Models.Indicator;
public class TrendiloParameter : IndicatorParameterBase
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