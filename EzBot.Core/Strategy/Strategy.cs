using EzBot.Core.Indicator;
using EzBot.Models;

namespace EzBot.Core.Strategy;

public abstract class Strategy : IStrategy
{
    protected List<IIndicator> Indicators { get; set; } = new List<IIndicator>();

    public Strategy(List<Parameter> parameters)
    {
        LoadIndicators(parameters);
    }

    public abstract TradeOrder GetAction(List<BarData> bars);

    protected abstract void LoadIndicators(List<Parameter> parameters);
}