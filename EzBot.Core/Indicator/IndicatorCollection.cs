using EzBot.Core.IndicatorParameter;
using EzBot.Core.Strategy;
using EzBot.Models;
using System.Collections;
using System.Collections.Concurrent;

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
            case StrategyType.PrecisionTrend:
                _indicators.Add(new Trendilo(new TrendiloParameter()));
                _indicators.Add(new NormalizedVolume(new NormalizedVolumeParameter()));
                _indicators.Add(new AtrBands(new AtrBandsParameter()));
                _indicators.Add(new Etma(new EtmaParameter()));
                break;
            default:
                throw new ArgumentException("Unknown StrategyType");
        }
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

    public void IncrementSingle()
    {
        foreach (IIndicator indicator in _indicators)
        {
            if (indicator.GetParameters().CanIncrement())
            {
                indicator.GetParameters().IncrementSingle();
                InvalidateCache();
                return;
            }
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
            var parameters = indicator.GetParameters();
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

    public HashSet<IndicatorCollection> GenerateAllParameterCombinations()
    {
        var indicatorTypes = new Dictionary<string, Type>();

        // First, pre-calculate all possible parameter combinations for each indicator type
        var parametersByIndicatorType = new Dictionary<string, List<IIndicatorParameter>>();

        // Calculate all parameter combinations for each indicator
        foreach (var indicator in _indicators)
        {
            string typeName = indicator.GetType().Name;
            if (!parametersByIndicatorType.ContainsKey(typeName))
            {
                indicatorTypes[typeName] = indicator.GetType();

                // Generate all possible parameter values for this indicator type
                var allParams = new List<IIndicatorParameter>();
                var baseParam = indicator.GetParameters();
                var clone = baseParam.DeepClone();

                // Add the initial state
                allParams.Add(clone);

                // Generate all possible parameter values for this indicator
                while (clone.CanIncrement())
                {
                    // Create a new clone to modify
                    clone = clone.DeepClone();
                    clone.IncrementSingle();
                    allParams.Add(clone);
                }

                parametersByIndicatorType[typeName] = allParams;
            }
        }

        // Now use CartesianProduct to generate all combinations without deep cloning each time
        var indicatorParameterSets = _indicators
            .Select(ind => parametersByIndicatorType[ind.GetType().Name])
            .ToList();

        // Generate the cartesian product of all parameter combinations
        var combinations = CartesianProduct(indicatorParameterSets).ToList();

        // Use a thread-safe collection for parallel processing
        var concurrentResult = new ConcurrentBag<IndicatorCollection>();

        // Determine if parallelization is worth it (small sets might be faster sequentially)
        bool useParallel = combinations.Count > 1000;

        if (useParallel)
        {
            // Configure parallel options for optimal performance
            var parallelOptions = new ParallelOptions
            {
                // Use available processor count but limit to avoid thread contention
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            Parallel.ForEach(combinations, parallelOptions, combination =>
            {
                var collection = new IndicatorCollection();

                for (int i = 0; i < _indicators.Count; i++)
                {
                    var indicatorType = _indicators[i].GetType();
                    var param = combination[i];
                    var indicator = (IIndicator)Activator.CreateInstance(indicatorType, param)!;
                    collection.Add(indicator);
                }

                concurrentResult.Add(collection);
            });

            return [.. concurrentResult];
        }
        else
        {
            // For smaller sets, use the original sequential implementation
            var result = new HashSet<IndicatorCollection>();

            foreach (var combination in combinations)
            {
                var collection = new IndicatorCollection();

                for (int i = 0; i < _indicators.Count; i++)
                {
                    var indicatorType = _indicators[i].GetType();
                    var param = combination[i];
                    var indicator = (IIndicator)Activator.CreateInstance(indicatorType, param)!;
                    collection.Add(indicator);
                }

                result.Add(collection);
            }

            return result;
        }
    }

    private static IEnumerable<List<T>> CartesianProduct<T>(List<List<T>> sequences)
    {
        IEnumerable<List<T>> emptyProduct = [[]];

        return sequences.Aggregate(
            emptyProduct,
            (accumulator, sequence) =>
                from acc in accumulator
                from item in sequence
                select acc.Concat([item]).ToList()
        );
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