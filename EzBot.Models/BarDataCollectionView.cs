
namespace EzBot.Models;

// Provides a view over a list of bar data without creating a new list
public class BarDataCollectionView : IBarDataCollection
{
    private readonly List<BarData> _source;
    private readonly int _startIndex;
    private readonly int _endIndex;
    private readonly int _count;

    public BarDataCollectionView(List<BarData> source, int startIndex, int endIndex)
    {
        _source = source;
        _startIndex = startIndex;
        _endIndex = endIndex;
        _count = endIndex - startIndex + 1;
    }

    public int Count => _count;

    public BarData this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _source[_startIndex + index];
        }
    }

    public List<double> GetValues(BarValueType valueType)
    {
        var result = new List<double>(_count);

        for (int i = _startIndex; i <= _endIndex; i++)
        {
            result.Add(GetValueFromBar(_source[i], valueType));
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
}