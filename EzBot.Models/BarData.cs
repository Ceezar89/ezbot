namespace EzBot.Models;
public class BarData
{
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public double Volume { get; set; }

    // Constructor
    public BarData(double open, double high, double low, double close, double volume)
    {
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
    }
}