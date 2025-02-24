
namespace EzBot.Models;
public class BarDataCollection(List<BarData> bars) : IEquatable<BarDataCollection>
{
    public List<BarData> Bars { get; } = bars;
    public int Signature { get; } = ComputeSignature(bars);

    private static int ComputeSignature(List<BarData> bars)
    {
        int hash = bars.Count;
        if (bars.Count > 0)
        {
            // Combine the count with the hash code of the most recent bar.
            hash = (hash * 397) ^ bars[^1].GetHashCode();
        }
        return hash;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as BarDataCollection);
    }

    public bool Equals(BarDataCollection? other)
    {
        if (other is null)
            return false;
        return Signature == other.Signature;
    }

    public override int GetHashCode() => Signature;
}