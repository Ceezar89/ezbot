namespace EzBot.Models;
public class BarData(double open, double high, double low, double close, double volume)
{
    public double Open { get; set; } = open;
    public double High { get; set; } = high;
    public double Low { get; set; } = low;
    public double Close { get; set; } = close;
    public double Volume { get; set; } = volume;

    // Default constructor for serialization
    public BarData() : this(0, 0, 0, 0, 0)
    {
    }
}