using EzBot.Models;
using EzBot.Models.Indicator;

namespace EzBot.Core.Indicator;

public interface IIndicator
{
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
}