using EzBot.Models;

namespace EzBot.Core.Indicator;

public interface IIndicator
{
    void Calculate(List<BarData> bars);
}

public interface IVolumeIndicator : IIndicator
{
    VolumeSignal GetSignal();
}

public interface ITrendIndicator : IIndicator
{
    TrendSignal GetSignal();
}

public interface IRiskManagementIndicator : IIndicator
{
    double GetStopLoss();
}