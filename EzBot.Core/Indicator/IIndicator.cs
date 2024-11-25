using EzBot.Models;

namespace EzBot.Core.Indicator;

public interface IIndicator
{
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