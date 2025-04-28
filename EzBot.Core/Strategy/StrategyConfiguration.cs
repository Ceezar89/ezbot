using EzBot.Core.Indicator;
using EzBot.Core.IndicatorParameter;

namespace EzBot.Core.Strategy;

public enum IndicatorType
{
    Etma,
    McGinley,
    Trendilo,
    NormalizedVolume,
    Supertrend,
    Lwpi,
    AtrBands
}

/// <summary>
/// Defines the configuration for a trading strategy including required indicators
/// </summary>
public class StrategyConfiguration()
{
    private readonly List<IIndicator> _indicators = [];

    public IReadOnlyList<IIndicator> Indicators => _indicators.AsReadOnly();

    public string Name { get; private set; } = "EmptyConfiguration";

    public StrategyConfiguration(List<IndicatorType> indicatorTypes) : this()
    {
        foreach (var indicatorType in indicatorTypes)
        {
            IIndicator indicator = indicatorType switch
            {
                IndicatorType.Etma => new Etma(new EtmaParameter()),
                IndicatorType.McGinley => new McGinley(new McGinleyParameter()),
                IndicatorType.Trendilo => new Trendilo(new TrendiloParameter()),
                IndicatorType.NormalizedVolume => new NormalizedVolume(new NormalizedVolumeParameter()),
                IndicatorType.Supertrend => new Supertrend(new SupertrendParameter()),
                IndicatorType.Lwpi => new Lwpi(new LwpiParameter()),
                IndicatorType.AtrBands => new AtrBands(new AtrBandsParameter()),
                _ => throw new ArgumentException($"Unsupported indicator type: {indicatorType}")
            };
            _indicators.Add(indicator);
        }

        // Add standard risk management indicator if not present
        if (!HasRiskManagementIndicator())
        {
            _indicators.Add(new AtrBands(new AtrBandsParameter()));
        }

        List<string> names = [];
        foreach (var indicator in _indicators)
        {
            // skip risk management indicators because every strategy has AtrBands
            if (indicator is not IRiskManagementIndicator)
                names.Add(indicator.GetType().Name);
        }
        names.Sort(); // prevent duplicate names if ordered differently
        Name = string.Concat(names);
    }

    public IndicatorCollection ToIndicatorCollection()
    {
        return new IndicatorCollection(_indicators);
    }

    private bool HasRiskManagementIndicator()
    {
        foreach (var indicator in _indicators)
        {
            if (indicator is IRiskManagementIndicator)
                return true;
        }
        return false;
    }
}