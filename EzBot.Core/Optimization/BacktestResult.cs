using EzBot.Models;

namespace EzBot.Core.Optimization;

public class BacktestResult
{
    public double InitialBalance { get; set; }
    public double FinalBalance { get; set; }
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public double MaxDrawdown { get; set; }
    public double SharpeRatio { get; set; }
    public long StartUnixTime { get; set; }
    public long EndUnixTime { get; set; }
    public int MaxDaysInactive { get; set; }
    public double RiskPercentage { get; set; }
    public int MaxConcurrentTrades { get; set; }
    public double FeePercentage { get; set; }
    public int Leverage { get; set; }
    public TimeFrame TimeFrame { get; set; }
    public int LookbackDays { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool TerminatedEarly { get; set; }
    public string TerminationReason { get; set; } = string.Empty;

    public double TotalReturn => FinalBalance - InitialBalance;
    public double ReturnPercentage => (FinalBalance / InitialBalance - 1) * 100;
    public double WinRate => TotalTrades > 0 ? (double)WinningTrades / TotalTrades : 0;

    // Compatibility properties to support existing StrategyTester code
    public double NetProfit => TotalReturn;
    public double WinRatePercent => WinRate * 100;
    public double TradingActivityPercentage => EndUnixTime > StartUnixTime
        ? 100 - (MaxDaysInactive * 100.0 / ((EndUnixTime - StartUnixTime) / (60 * 60 * 24)))
        : 0;
}