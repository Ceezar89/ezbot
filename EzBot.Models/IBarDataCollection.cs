namespace EzBot.Models;

// Interface for bar data collections
public interface IBarDataCollection
{
    int Count { get; }
    BarData this[int index] { get; }
    List<double> GetValues(BarValueType valueType);
}