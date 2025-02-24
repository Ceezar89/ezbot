using System.Collections;
using EzBot.Models;

namespace EzBot.Core.Indicator;

public class IndicatorCollection : IEnumerable<IIndicator>, IEquatable<IndicatorCollection>
{
    private readonly List<IIndicator> _indicators;
    private int? _hashCode;

    public IndicatorCollection()
    {
        _indicators = [];
    }

    public IndicatorCollection(IEnumerable<IIndicator> indicators)
    {
        _indicators = [.. indicators];
    }

    public void Add(IIndicator indicator)
    {
        _indicators.Add(indicator);
        InvalidateCache();
    }

    public bool Remove(IIndicator indicator)
    {
        var result = _indicators.Remove(indicator);
        if (result)
            InvalidateCache();
        return result;
    }

    public int Count => _indicators.Count;
    public IIndicator this[int index] => _indicators[index];

    public bool CanIncrement()
    {
        foreach (IIndicator indicator in _indicators)
        {
            if (indicator.GetParameters().CanIncrement())
                return true;
        }
        return false;
    }

    public void UpdateAll(List<BarData> bars)
    {
        BarDataCollection barDataCollection = new(bars); // collection hashes the bars for lazy evaluation
        foreach (var indicator in _indicators)
        {
            indicator.SetBarData(barDataCollection);
        }
    }

    public IndicatorCollection DeepClone()
    {
        var cloneList = new List<IIndicator>();
        foreach (var indicator in _indicators)
        {
            // find the type of the indicator and create a new instance of it
            var type = indicator.GetType();
            var parameters = indicator.GetParameters();
            // create a new instance of the indicator
            var newIndicator = (IIndicator)Activator.CreateInstance(type, parameters)!;
            cloneList.Add(newIndicator);
        }
        return [.. cloneList];
    }

    public IEnumerator<IIndicator> GetEnumerator() => _indicators.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _indicators.GetEnumerator();
    public override bool Equals(object? obj) => Equals(obj as IndicatorCollection);

    public bool Equals(IndicatorCollection? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Count != other.Count) return false;
        for (int i = 0; i < _indicators.Count; i++)
        {
            if (!_indicators[i].Equals(other._indicators[i]))
                return false;
        }
        return true;
    }

    public override int GetHashCode()
    {
        if (_hashCode.HasValue)
            return _hashCode.Value;

        var hash = new HashCode();
        foreach (var indicator in _indicators.OrderBy(i => i.GetType().FullName))
        {
            hash.Add(indicator.GetType().FullName);
            hash.Add(indicator.GetParameters().GetHashCode());
        }
        _hashCode = hash.ToHashCode();
        return _hashCode.Value;
    }

    public void InvalidateCache()
    {
        _hashCode = null;
    }
}