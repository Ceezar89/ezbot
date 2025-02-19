namespace EzBot.Models;

public record BarData(
    long Timestamp = default,
    double Open = default,
    double High = default,
    double Low = default,
    double Close = default,
    double Volume = default)
{
    // unix timestamp as int
    public long TimeStamp { get; set; } = Timestamp;
    public double Open { get; set; } = Open;
    public double High { get; set; } = High;
    public double Low { get; set; } = Low;
    public double Close { get; set; } = Close;
    public double Volume { get; set; } = Volume;

}