using EzBot.Models;

namespace EzBot.Core.Optimization;

/// <summary>
/// Simulates a trading account during backtesting
/// </summary>
public class BacktestAccount
{
    // Account state
    public double CurrentBalance { get; private set; }
    public double InitialBalance { get; }
    public double FeePercentage { get; }
    public int Leverage { get; }

    // Performance metrics
    public int MaxDaysInactive { get; set; }
    public double DrawdownPercentage => peakBalance > 0 ? Math.Max(0, (peakBalance - CurrentBalance) / peakBalance) : 0;

    // Time tracking
    public long StartUnixTime { get; set; }
    public long EndUnixTime { get; set; }

    // Tracking for position management
    private int nextTradeId = 1;
    private double peakBalance;
    private double totalGainAmount;
    private double totalLossAmount;
    private int winCount;
    private int lossCount;
    private const double RiskPercentage = 0.01; // 1% risk per trade

    // Historical balance tracking
    private readonly Dictionary<int, double> balanceHistory = new Dictionary<int, double>();

    // Trade management - use Dictionary for O(1) lookups by ID instead of list searches
    private readonly Dictionary<int, BacktestTrade> trades;

    // Precomputed fee calculations
    private readonly double feeFactor;

    public BacktestAccount(double initialBalance, double feePercentage, int leverage)
    {
        InitialBalance = initialBalance;
        CurrentBalance = initialBalance;
        peakBalance = initialBalance;
        FeePercentage = feePercentage;
        Leverage = leverage;

        // Pre-compute fee multiplier for faster calculations
        feeFactor = 1.0 - (feePercentage / 100.0);

        // Initialize trade tracking with expected capacity
        trades = new Dictionary<int, BacktestTrade>(100); // Typical backtest capacity
    }

    /// <summary>
    /// Opens a position and returns the trade ID
    /// </summary>
    public int OpenPosition(TradeType type, double price, double stopLoss, int barIndex)
    {
        // Track balance at this bar
        balanceHistory[barIndex] = CurrentBalance;

        // Calculate position size based on fixed percentage risk management
        double riskAmount = CurrentBalance * RiskPercentage; // 1% of current balance at risk
        double priceDifference = Math.Abs(price - stopLoss);

        // Validate stop loss is set properly
        if (priceDifference <= 0 || priceDifference > price * 0.1) // Prevent division by zero and unreasonable stops
        {
            // If stop is too far or invalid, use default 1% stop distance
            priceDifference = price * 0.01;
        }

        // Calculate position size: risk amount / (price difference / leverage)
        double positionSize = riskAmount / (priceDifference / Leverage);

        // Calculate the actual amount at risk - should be exactly 1% of balance
        double actualRiskAmount = positionSize * priceDifference / Leverage;

        // Calculate margin required
        double marginUsed = price * positionSize / Leverage;

        // Calculate entry fee
        double entryFee = price * positionSize * (FeePercentage / 100.0);

        // Create the trade with all necessary information
        var trade = new BacktestTrade
        {
            Id = nextTradeId,
            Type = type,
            EntryPrice = price,
            EntryBar = barIndex,
            StopLoss = stopLoss,
            Quantity = positionSize,
            EntryAmount = entryFee,
            MarginUsed = marginUsed,
            RiskAmount = actualRiskAmount
        };

        // Subtract only the entry fee from the balance
        // (margin is only reserved, not deducted from balance)
        CurrentBalance -= entryFee;

        // Store the trade with its ID
        int tradeId = nextTradeId++;
        trades[tradeId] = trade;

        return tradeId;
    }

    /// <summary>
    /// Closes a position and updates account metrics
    /// </summary>
    public void ClosePosition(int tradeId, double exitPrice, int barIndex)
    {
        // Fast lookup via dictionary
        if (!trades.TryGetValue(tradeId, out var trade))
            return;

        // Skip already closed trades
        if (trade.IsClosed)
            return;

        // Calculate raw P&L based on price movement and position size
        double rawPnl = CalculatePnl(trade.Type, trade.EntryPrice, exitPrice, trade.Quantity);

        // Calculate exit fee
        double exitFee = exitPrice * trade.Quantity * (FeePercentage / 100.0);

        // Net P&L after fees
        double netPnl = rawPnl - exitFee;

        // Update trade data
        trade.ExitPrice = exitPrice;
        trade.ExitBar = barIndex;
        trade.ExitAmount = exitFee;
        trade.IsClosed = true;
        trade.Profit = netPnl;

        // Update account balance with the net P&L
        CurrentBalance += netPnl;

        // Update performance metrics
        if (netPnl > 0)
        {
            winCount++;
            totalGainAmount += netPnl;
        }
        else
        {
            lossCount++;
            totalLossAmount += Math.Abs(netPnl);
        }

        // Update peak balance for drawdown calculations
        if (CurrentBalance > peakBalance)
            peakBalance = CurrentBalance;

        // Track balance at this bar
        balanceHistory[barIndex] = CurrentBalance;
    }

    /// <summary>
    /// Returns trade information for the given ID (or null if not found)
    /// </summary>
    public BacktestTrade? GetTradeById(int tradeId)
    {
        trades.TryGetValue(tradeId, out var trade);
        return trade;
    }

    /// <summary>
    /// Calculate raw profit/loss (before fees)
    /// </summary>
    private static double CalculatePnl(TradeType tradeType, double entryPrice, double exitPrice, double quantity)
    {
        return tradeType == TradeType.Long
            ? (exitPrice - entryPrice) * quantity
            : (entryPrice - exitPrice) * quantity;
    }

    /// <summary>
    /// Generate a backtest result summary
    /// </summary>
    public BacktestResult GenerateResult()
    {
        // Extract closed trades for analysis
        var closedTrades = trades.Values.Where(t => t.IsClosed).ToArray();
        int totalTrades = closedTrades.Length;

        // Get win/loss count for the result - match property names
        int winningTrades = winCount;
        int losingTrades = lossCount;

        // Calculate Sharpe ratio
        double sharpeRatio = 0;
        if (totalTrades > 0)
        {
            double mean = closedTrades.Average(t => t.Profit);
            double stdDev = Math.Sqrt(closedTrades.Sum(t => Math.Pow(t.Profit - mean, 2)) / totalTrades);
            sharpeRatio = stdDev == 0 ? 0 : mean / stdDev;
        }

        // Avoid division by zero
        if (totalTrades == 0)
        {
            return new BacktestResult
            {
                InitialBalance = InitialBalance,
                FinalBalance = CurrentBalance,
                TotalTrades = 0,
                WinningTrades = 0,
                LosingTrades = 0,
                MaxDrawdown = 0,
                SharpeRatio = 0,
                StartUnixTime = StartUnixTime,
                EndUnixTime = EndUnixTime,
                MaxDaysInactive = MaxDaysInactive,
                CompletionPercentage = 1.0 // Set default completion to 100%
            };
        }

        // Calculate maximum drawdown
        double drawdown = DrawdownPercentage;

        // Create and return the result
        return new BacktestResult
        {
            InitialBalance = InitialBalance,
            FinalBalance = CurrentBalance,
            TotalTrades = totalTrades,
            WinningTrades = winningTrades,
            LosingTrades = losingTrades,
            MaxDrawdown = drawdown,
            SharpeRatio = sharpeRatio,
            StartUnixTime = StartUnixTime,
            EndUnixTime = EndUnixTime,
            MaxDaysInactive = MaxDaysInactive,
            CompletionPercentage = 1.0 // Set default completion to 100%
        };
    }

    /// <summary>
    /// Validates that the actual risk is within tolerance of the target risk percentage
    /// </summary>
    public void ValidateRisk(int tradeId)
    {
        if (trades.TryGetValue(tradeId, out var trade))
        {
            double targetRiskAmount = InitialBalance * RiskPercentage;
            double actualRiskAmount = trade.RiskAmount;
            double riskRatio = actualRiskAmount / targetRiskAmount;

            // Validate risk is within 1% of target (0.99 to 1.01 times target)
            bool isRiskCorrect = riskRatio >= 0.99 && riskRatio <= 1.01;

            if (!isRiskCorrect)
            {
                // For debugging
                Console.WriteLine($"Trade {tradeId} risk validation failed:");
                Console.WriteLine($"  Target risk: {targetRiskAmount:F2}");
                Console.WriteLine($"  Actual risk: {actualRiskAmount:F2}");
                Console.WriteLine($"  Ratio: {riskRatio:F4}");
            }
        }
    }

    /// <summary>
    /// Gets the historical balance at a specific bar index, or the initial balance if not recorded
    /// </summary>
    public double GetHistoricalBalance(int barIndex)
    {
        // If we have the exact bar, return it
        if (balanceHistory.TryGetValue(barIndex, out double balance))
            return balance;

        // Otherwise find the closest previous bar
        int closestBar = -1;
        foreach (int bar in balanceHistory.Keys)
        {
            if (bar <= barIndex && bar > closestBar)
                closestBar = bar;
        }

        // If we found a previous bar, return its balance
        if (closestBar >= 0)
            return balanceHistory[closestBar];

        // Otherwise return initial balance
        return InitialBalance;
    }
}

/// <summary>
/// Represents a simulated trade during backtesting
/// </summary>
public class BacktestTrade
{
    public int Id { get; set; }
    public TradeType Type { get; set; }
    public double EntryPrice { get; set; }
    public double ExitPrice { get; set; }
    public double Quantity { get; set; }
    public double StopLoss { get; set; }
    public int EntryBar { get; set; }
    public int ExitBar { get; set; }
    public double EntryAmount { get; set; }
    public double ExitAmount { get; set; }
    public double Profit { get; set; }
    public bool IsClosed { get; set; }
    public double MarginUsed { get; set; }
    public double RiskAmount { get; set; }
}