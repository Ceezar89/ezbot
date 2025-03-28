using EzBot.Models;
using EzBot.Core.IndicatorParameter;

namespace EzBot.Core.Indicator;

public abstract class IndicatorBase<TParameter>(TParameter parameter) : IIndicator
    where TParameter : IIndicatorParameter
{
    protected TParameter Parameter { get; private set; } = parameter;
    private int? lastProcessedSignature;

    public void SetBarData(IBarDataCollection bars)
    {
        if (bars is BarDataCollection collection)
        {
            if (lastProcessedSignature is null || lastProcessedSignature != collection.Signature)
            {
                ProcessBarData(collection.Bars);
                lastProcessedSignature = collection.Signature;
            }
        }
        else
        {
            // For other implementations like BarDataCollectionView, process directly
            var barsList = new List<BarData>(bars.Count);
            for (int i = 0; i < bars.Count; i++)
            {
                barsList.Add(bars[i]);
            }
            ProcessBarData(barsList);
        }
    }

    protected abstract void ProcessBarData(List<BarData> bars);

    public IIndicatorParameter GetParameters() => Parameter;

    public void UpdateParameters(IIndicatorParameter parameter)
    {
        Parameter = (TParameter)parameter;
    }
}