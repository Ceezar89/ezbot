namespace EzBot.Models;

public class BarDataCollection(List<BarData> bars) : IEquatable<BarDataCollection>, IBarDataCollection
{
    public List<BarData> Bars { get; } = bars;
    public int Signature { get; } = ComputeSignature(bars);

    // IBarDataCollection implementation
    public int Count => Bars.Count;

    public BarData this[int index] => Bars[index];

    public List<double> GetValues(BarValueType valueType)
    {
        var result = new List<double>(Bars.Count);

        foreach (var bar in Bars)
        {
            result.Add(GetValueFromBar(bar, valueType));
        }

        return result;
    }

    private static double GetValueFromBar(BarData bar, BarValueType valueType) => valueType switch
    {
        BarValueType.Open => bar.Open,
        BarValueType.High => bar.High,
        BarValueType.Low => bar.Low,
        BarValueType.Close => bar.Close,
        BarValueType.Volume => bar.Volume,
        BarValueType.HL2 => (bar.High + bar.Low) / 2.0,
        BarValueType.HLC3 => (bar.High + bar.Low + bar.Close) / 3.0,
        BarValueType.OHLC4 => (bar.Open + bar.High + bar.Low + bar.Close) / 4.0,
        _ => throw new ArgumentOutOfRangeException(nameof(valueType))
    };

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