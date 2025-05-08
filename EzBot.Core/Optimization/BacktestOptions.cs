using EzBot.Models;

namespace EzBot.Core.Optimization;

public class BacktestOptions
{
    public double InitialBalance { get; set; } = 1000;
    public double FeePercentage { get; set; } = 0.05;
    public int Leverage { get; set; } = 10;
    public TimeFrame TimeFrame { get; set; } = TimeFrame.OneHour;
    public int LookbackDays { get; set; } = 1500;
    public double MaxDrawdown { get; set; } = 0.3;
    public List<int> MaxConcurrentTrades { get; set; } = [1];
    public List<double> RiskPercentage { get; set; } = [1.0];
    public double MaxInactivityPercentage { get; set; } = 0.05;

    public BacktestOptions(
        double initialBalance,
        double feePercentage,
        int leverage,
        TimeFrame timeFrame,
        int lookbackDays,
        double maxDrawdown,
        List<int>? maxConcurrentTrades,
        List<double>? riskPercentage,
        double maxInactivityPercentage
    )
    {
        InitialBalance = initialBalance;
        FeePercentage = feePercentage;
        Leverage = leverage;
        TimeFrame = timeFrame;
        LookbackDays = lookbackDays;
        MaxDrawdown = maxDrawdown;
        MaxConcurrentTrades = maxConcurrentTrades ?? [1];
        RiskPercentage = riskPercentage ?? [1.0];
        MaxInactivityPercentage = maxInactivityPercentage;
    }
}
