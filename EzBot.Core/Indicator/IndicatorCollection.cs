using EzBot.Core.Strategy;
using EzBot.Models;

namespace EzBot.Core.Indicator;

public class IndicatorCollection
{
    private readonly List<IIndicator> _indicators;

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
    }

    public int Count => _indicators.Count;
    public IIndicator this[int index] => _indicators[index];

    public static List<IndicatorCollection> GenerateAllPermutations(StrategyConfiguration configuration)
    {
        Dictionary<string, IndicatorCollection> indicatorCollections = [];
        var collection = configuration.ToIndicatorCollection();
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

    // Increments to the next parameter combination
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
        return new IndicatorCollection(cloneList);
    }

    // for non brute force optimization
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
    }

    public IEnumerator<IIndicator> GetEnumerator() => _indicators.GetEnumerator();
}