
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
    public int MaxConcurrentTrades { get; set; } = 1;
    public int MaxDaysInactive { get; set; } = 7;

    public BacktestOptions(double initialBalance, double feePercentage, int leverage, TimeFrame timeFrame, int lookbackDays, double maxDrawdown, int maxConcurrentTrades, int maxDaysInactive)
    {
        InitialBalance = initialBalance;
        FeePercentage = feePercentage;
        Leverage = leverage;
        TimeFrame = timeFrame;
        LookbackDays = lookbackDays;
        MaxDrawdown = maxDrawdown;
        MaxConcurrentTrades = maxConcurrentTrades;
        MaxDaysInactive = maxDaysInactive;
    }
}
