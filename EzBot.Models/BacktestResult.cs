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
    public double MaxDrawdown { get; set; }
    public double WinRate => TotalTrades > 0 ? (double)WinningTrades / TotalTrades : 0;
    public double WinRatePercent => TotalTrades > 0 ? (double)WinningTrades / TotalTrades * 100 : 0;
    public double SharpeRatio { get; set; }
    public int MaxConcurrentTrades { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int BacktestDurationDays { get; set; }
    public int MaxDaysInactive { get; set; }
    public bool TerminatedEarly { get; set; } = false;

    // ADDED: Store Unix timestamps for exact calculations
    public long StartUnixTime { get; set; }
    public long EndUnixTime { get; set; }

    // ADDED: Calculate how much of the backtest period was actually used
    public double TradingActivityPercentage
    {
        get
        {
            // If no trades or invalid dates, return 0
            if (TotalTrades == 0 || EndUnixTime <= StartUnixTime)
                return 0;

            // Calculate active trading duration as a percentage of total duration
            double totalDuration = (EndUnixTime - StartUnixTime) / (24.0 * 60 * 60);
            double inactiveDays = MaxDaysInactive;

            // If inactive days exceeds total duration, something is wrong
            if (inactiveDays >= totalDuration)
                return 0;

            // Return percentage of time that was active for trading
            return Math.Max(0, Math.Min(100, 100 * (1.0 - inactiveDays / totalDuration)));
        }
    }
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