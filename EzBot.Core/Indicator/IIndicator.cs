using EzBot.Models;
using EzBot.Core.IndicatorParameter;

namespace EzBot.Core.Indicator;

public interface IIndicator
{
    IIndicatorParameter GetParameters();
    void UpdateParameters(IIndicatorParameter parameter);
    void Calculate(List<BarData> bars);
}

public interface IVolumeIndicator : IIndicator
{
    VolumeSignal GetVolumeSignal();
}

public interface ITrendIndicator : IIndicator
{
    TrendSignal GetTrendSignal();
}

public interface IRiskManagementIndicator : IIndicator
{
    double GetLongStopLoss();
    double GetShortStopLoss();
    double GetShortTakeProfit();
    double GetLongTakeProfit();
}