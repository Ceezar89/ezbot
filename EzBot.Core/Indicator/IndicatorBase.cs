using EzBot.Models;
using EzBot.Core.IndicatorParameter;

namespace EzBot.Core.Indicator;

public abstract class IndicatorBase<TParameter>(TParameter parameter) : IIndicator where TParameter : IIndicatorParameter
{
    protected TParameter Parameter { get; private set; } = parameter;

    public IIndicatorParameter GetParameters() => Parameter;

    public void UpdateParameters(IIndicatorParameter parameter)
    {
        Parameter = (TParameter)parameter;
    }

    public abstract void Calculate(List<BarData> bars);
}