using EzBot.Models;
using EzBot.Core.IndicatorParameter;

namespace EzBot.Core.Indicator;

public abstract class IndicatorBase<TParameter>(TParameter parameter) : IIndicator
    where TParameter : IIndicatorParameter
{
    protected TParameter Parameter { get; private set; } = parameter;
    private int? lastProcessedSignature;

    public void SetBarData(BarDataCollection bars)
    {
        if (lastProcessedSignature is null || lastProcessedSignature != bars.Signature)
        {
            ProcessBarData(bars.Bars);
            lastProcessedSignature = bars.Signature;
        }
    }

    protected abstract void ProcessBarData(List<BarData> bars);

    public IIndicatorParameter GetParameters() => Parameter;

    public void UpdateParameters(IIndicatorParameter parameter)
    {
        Parameter = (TParameter)parameter;
    }
}