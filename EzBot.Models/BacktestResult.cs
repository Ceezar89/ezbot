namespace EzBot.Models;
public class BacktestResult
{
    public double InitialBalance { get; set; }
    public double FinalBalance { get; set; }
    public double NetProfit => FinalBalance - InitialBalance;
    public double ReturnPercentage => NetProfit / InitialBalance * 100;
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public double MaxDrawdownPercent { get; set; }
    public double WinRate => TotalTrades > 0 ? (double)WinningTrades / TotalTrades : 0;
    public double WinRatePercent => TotalTrades > 0 ? (double)WinningTrades / TotalTrades * 100 : 0;
    public double MaxDrawdown { get; set; }
    public double SharpeRatio { get; set; }
    public int MaxConcurrentTrades { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int BacktestDurationDays { get; set; }
    public int MaxDaysInactive { get; set; }
}

public class BacktestTrade
{
    public TradeType Type { get; set; }
    public double EntryPrice { get; set; }
    public double ExitPrice { get; set; }
    public double StopLoss { get; set; }
    public int EntryBar { get; set; }
    public int ExitBar { get; set; }
    public double Profit { get; set; }
    public double PositionSize { get; set; }
    public bool IsWinner => Profit > 0;
}