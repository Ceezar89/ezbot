using EzBot.Core.IndicatorParameter;
using EzBot.Core.Strategy;
using EzBot.Models;
using System.Collections;

namespace EzBot.Core.Indicator;

public class IndicatorCollection : IEnumerable<IIndicator>, IEquatable<IndicatorCollection>
{
    private readonly List<IIndicator> _indicators;
    private int? _hashCode;

    public IndicatorCollection()
    {
        _indicators = [];
    }

    public IndicatorCollection(StrategyType strategyType)
    {
        _indicators = [];

        switch (strategyType)
        {
            case StrategyType.TrendsAndVolume:
                _indicators.Add(new Etma(new EtmaParameter()));
                _indicators.Add(new NormalizedVolume(new NormalizedVolumeParameter()));
                _indicators.Add(new Trendilo(new TrendiloParameter()));
                break;
            case StrategyType.McGinleyTrend:
                _indicators.Add(new McGinleyDynamic(new McGinleyDynamicParameter()));
                _indicators.Add(new Lwpi(new LwpiParameter()));
                // _indicators.Add(new Trendilo(new TrendiloParameter()));
                break;
            case StrategyType.EtmaTrend:
                _indicators.Add(new Etma(new EtmaParameter()));
                _indicators.Add(new Lwpi(new LwpiParameter()));
                // _indicators.Add(new Trendilo(new TrendiloParameter()));
                break;
            default:
                throw new ArgumentException("Unknown StrategyType");
        }
        // Always add AtrBands to the collection
        _indicators.Add(new AtrBands(new AtrBandsParameter()));
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

    public int Count => _indicators.Count;
    public IIndicator this[int index] => _indicators[index];

    public static List<IndicatorCollection> GenerateAllPermutations(StrategyType strategyType)
    {
        Dictionary<string, IndicatorCollection> indicatorCollections = [];
        var collection = new IndicatorCollection(strategyType);
        collection.Reset();

        // add the first collection to the dictionary
        string key = GenerateParameterKey(collection);
        indicatorCollections.Add(key, collection.DeepClone());
        while (collection.Next())
        {
            key = GenerateParameterKey(collection);
            indicatorCollections.Add(key, collection.DeepClone());
        }
        return [.. indicatorCollections.Values];
    }

    public static string GenerateParameterKey(IndicatorCollection parameters)
    {
        // Create a string representation of parameter values
        var key = new System.Text.StringBuilder();

        foreach (var indicator in parameters)
        {
            var indParams = indicator.GetParameters();
            var props = indParams.GetProperties();

            // Add indicator type to key
            key.Append(indicator.GetType().Name).Append(':');

            // Add each parameter value to key
            foreach (var prop in props)
            {
                key.Append(prop.Name).Append('=').Append(prop.Value).Append(',');
            }

            key.Append(';');
        }

        return key.ToString();
    }

    // Advances to the next parameter combination
    // Returns true if successful, false if we've exhausted all combinations
    public bool Next()
    {
        if (_indicators.Count == 0)
            return false;

        foreach (var indicator in _indicators)
        {
            var parameters = indicator.GetParameters();
            if (parameters.IncrementSingle())
            {
                return true;
            }
            parameters.Reset();
        }
        return false;
    }

    // Reset the iteration to the beginning
    public void Reset()
    {
        // Reset all indicators to their initial parameter state
        foreach (IIndicator indicator in _indicators)
        {
            indicator.GetParameters().Reset();
        }
        InvalidateCache();
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
            var parameters = indicator.GetParameters().DeepClone();
            // create a new instance of the indicator
            var newIndicator = (IIndicator)Activator.CreateInstance(type, parameters)!;
            cloneList.Add(newIndicator);
        }
        return [.. cloneList];
    }

    public void RandomizeParameters()
    {
        Random random = new(Guid.NewGuid().GetHashCode());

        if (_indicators.Count == 0)
            throw new InvalidOperationException("Cannot randomize parameters for an empty collection");

        foreach (var indicator in _indicators)
        {
            var parameters = indicator.GetParameters();
            var randomNeighbor = parameters.GetRandomNeighbor(random);
            indicator.UpdateParameters(randomNeighbor);
        }
        InvalidateCache();
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

        // Use HashCode struct for modern hash combining
        var hash = new HashCode();

        // Add the type name and count of indicators to the hash
        hash.Add(_indicators.Count);

        foreach (var indicator in _indicators)
        {
            // Add the fully qualified name of each indicator type
            hash.Add(indicator.GetType().FullName);

            // Now hash all parameter values directly
            var parameters = indicator.GetParameters();
            var properties = parameters.GetProperties();

            foreach (var prop in properties)
            {
                // Include the name in the hash
                hash.Add(prop.Name);

                // Include the actual value based on its type
                if (prop.Value is int intValue)
                {
                    hash.Add(intValue);
                }
                else if (prop.Value is double doubleValue)
                {
                    hash.Add(doubleValue);
                }
                else
                {
                    // For any other type, use ToString
                    hash.Add(prop.Value?.ToString() ?? "null");
                }
            }
        }
        _hashCode = hash.ToHashCode();
        return _hashCode.Value;
    }

    public void InvalidateCache()
    {
        _hashCode = null;
    }
}