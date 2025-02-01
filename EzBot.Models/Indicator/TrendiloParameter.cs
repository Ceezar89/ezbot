namespace EzBot.Models.Indicator;
public class TrendiloParameter : IIndicatorParameter
{
    public string Id { get; set; }
    public int Smoothing { get; set; } = 1;
    public int Lookback { get; set; } = 50;
    public double AlmaOffset { get; set; } = 0.85;
    public int AlmaSigma { get; set; } = 6;
    public double BandMultiplier { get; set; } = 1.0;

    public TrendiloParameter(string id, int smoothing, int lookback, double almaOffset, int almaSigma, double bandMultiplier)
    {
        Id = id;
        Smoothing = smoothing;
        Lookback = lookback;
        AlmaOffset = almaOffset;
        AlmaSigma = almaSigma;
        BandMultiplier = bandMultiplier;
    }

    public TrendiloParameter(string id)
    {
        Id = id;
    }

}