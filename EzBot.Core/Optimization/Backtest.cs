using EzBot.Core.Strategy;
using EzBot.Models;

namespace EzBot.Core.Optimization;

public static class Backtest
{
    private const int WarmupPeriod = 30;

    public static BacktestResult Run(ITradingStrategy strategy, List<BarData> historicalData, BacktestOptions opt)
    {
        var account = new BacktestAccount(opt.InitialBalance, opt.FeePercentage, opt.Leverage);
        // Use a capacity-initialized dictionary to avoid resizing
        Dictionary<int, TradeOrder> activeOrders = new(opt.MaxConcurrentTrades);

        // Skip some initial bars to allow indicators to initialize
        if (historicalData.Count <= WarmupPeriod)
            throw new ArgumentException("Not enough data for backtesting");

        // Track last trade activity
        int lastTradeBarIndex = WarmupPeriod; // Initialize to the first bar after warmup
        int inactiveDaysCount = 0;
        double continuousInactivityDays = 0; // Track continuous inactivity

        // Calculate how many bars represent one day based on timeframe
        double barsPerDay = 24.0 * 60.0 / (int)opt.TimeFrame;

        account.StartUnixTime = historicalData[WarmupPeriod].TimeStamp;

        // Track performance metrics for early termination
        int consecutiveLosses = 0;
        int totalTrades = 0;
        double peakBalance = opt.InitialBalance;

        // Pre-allocate collections that are reused in the loop to reduce allocations
        var sortedTrades = new List<(int TradeId, TradeOrder Order, BacktestTrade? Trade)>(opt.MaxConcurrentTrades);
        var tradesClosedThisBar = new List<int>(opt.MaxConcurrentTrades);

        // Cache frequently used values
        int maxConcurrentTrades = opt.MaxConcurrentTrades;
        double maxDrawdown = opt.MaxDrawdown;
        int maxDaysInactive = opt.MaxDaysInactive;
        int historicalDataCount = historicalData.Count;
        int lastIndex = historicalDataCount - 1;

        for (int i = WarmupPeriod; i < historicalDataCount; i++)
        {
            var currentBar = historicalData[i];
            account.EndUnixTime = currentBar.TimeStamp;
            bool hadTradeActivity = false;

            // Clear collections instead of recreating them each iteration
            sortedTrades.Clear();
            tradesClosedThisBar.Clear();

            // Fast path for empty active orders
            if (activeOrders.Count > 0)
            {
                // Get all active trades and add them to the pre-allocated collection
                foreach (var kvp in activeOrders)
                {
                    var trade = account.GetTradeById(kvp.Key);
                    if (trade != null)
                    {
                        sortedTrades.Add((kvp.Key, kvp.Value, trade));
                    }
                }

                // Only sort if we have multiple trades - optimization for common case
                if (sortedTrades.Count > 1)
                {
                    // Sort in-place by entry bar
                    sortedTrades.Sort((a, b) => a.Trade!.EntryBar.CompareTo(b.Trade!.EntryBar));
                }

                // Process trades in order (oldest first)
                for (int t = 0; t < sortedTrades.Count; t++)
                {
                    var item = sortedTrades[t];
                    int tradeId = item.TradeId;
                    var order = item.Order;
                    var trade = item.Trade;

                    if (trade == null) continue;

                    bool closed = false;
                    double currentLow = currentBar.Low;
                    double currentHigh = currentBar.High;
                    double stopLoss = trade.StopLoss;
                    double takeProfit = order.TakeProfit;

                    // First check for stop loss, then take profit - simulating price movement sequentially
                    // This is more conservative approach, assuming stop loss is hit before take profit
                    if (order.TradeType == TradeType.Long)
                    {
                        if (currentLow <= stopLoss)
                        {
                            account.ClosePosition(tradeId, stopLoss, i);
                            closed = true;
                            consecutiveLosses++;
                            totalTrades++;
                        }
                        // Only check for take profit if stop loss wasn't hit
                        else if (currentHigh >= takeProfit)
                        {
                            account.ClosePosition(tradeId, takeProfit, i);
                            closed = true;
                            consecutiveLosses = 0;
                            totalTrades++;
                        }
                    }
                    else if (order.TradeType == TradeType.Short)
                    {
                        if (currentHigh >= stopLoss)
                        {
                            account.ClosePosition(tradeId, stopLoss, i);
                            closed = true;
                            consecutiveLosses++;
                            totalTrades++;
                        }
                        // Only check for take profit if stop loss wasn't hit
                        else if (currentLow <= takeProfit)
                        {
                            account.ClosePosition(tradeId, takeProfit, i);
                            closed = true;
                            consecutiveLosses = 0;
                            totalTrades++;
                        }
                    }

                    if (closed)
                    {
                        tradesClosedThisBar.Add(tradeId);
                        hadTradeActivity = true;
                    }
                }

                // Remove closed trades after processing them all
                if (tradesClosedThisBar.Count > 0)
                {
                    foreach (var tradeId in tradesClosedThisBar)
                    {
                        activeOrders.Remove(tradeId);
                    }
                }
            }

            // Check if we can open new positions - adjust position size based on how many concurrent trades we have
            int activeOrdersCount = activeOrders.Count;
            if (activeOrdersCount < maxConcurrentTrades)
            {
                try
                {
                    // Pass the current index to the strategy instead of creating a new list
                    var tradeOrder = strategy.GetAction(historicalData, i);

                    if (tradeOrder.TradeType != TradeType.None)
                    {
                        int tradeId = account.OpenPosition(tradeOrder.TradeType,
                            currentBar.Close, tradeOrder.StopLoss, i);
                        activeOrders.Add(tradeId, tradeOrder);

                        // Update last trade activity time
                        lastTradeBarIndex = i;
                        hadTradeActivity = true;
                    }
                }
                catch (Exception)
                {
                    // Skip problematic trade signals rather than failing the entire backtest
                    continue;
                }
            }

            // Update inactivity tracking
            if (hadTradeActivity)
            {
                // Reset continuous inactivity when we have activity
                continuousInactivityDays = 0;
                lastTradeBarIndex = i;
            }
            else if (activeOrders.Count > 0)
            {
                // We have open trades, so we're not inactive
                continuousInactivityDays = 0;
                lastTradeBarIndex = i;
            }
            else
            {
                // Calculate inactivity directly without repeated division
                continuousInactivityDays = (i - lastTradeBarIndex) / barsPerDay;

                // Update max days inactive using direct comparison
                int currentInactiveDays = (int)Math.Floor(continuousInactivityDays);
                if (currentInactiveDays > inactiveDaysCount)
                    inactiveDaysCount = currentInactiveDays;
            }

            // Update the maximum days inactive in the account
            account.MaxDaysInactive = inactiveDaysCount;

            // Calculate current drawdown
            double currentBalance = account.CurrentBalance;
            if (currentBalance > peakBalance)
                peakBalance = currentBalance;

            double currentDrawdown = (peakBalance - currentBalance) / peakBalance;

            // Early termination check for obviously poor strategies
            // Only apply after we've processed enough bars to have statistically significant results
            // Disabled entirely - we'll let all strategies complete 
            if (i > WarmupPeriod)
            {
                string terminationReason = string.Empty;

                if (currentDrawdown > maxDrawdown)
                {
                    terminationReason = $"Excessive drawdown: {currentDrawdown:P2} > {maxDrawdown:P2}";
                }
                else if (inactiveDaysCount > maxDaysInactive)
                {
                    terminationReason = $"Extended inactivity: {inactiveDaysCount} days > {maxDaysInactive} days";
                }

                if (!string.IsNullOrEmpty(terminationReason))
                {
                    var earlyTerminationResult = account.GenerateResult();
                    earlyTerminationResult.TerminatedEarly = true;
                    earlyTerminationResult.TerminationReason = terminationReason;
                    return earlyTerminationResult;
                }
            }
        }

        // Close any remaining positions at the end
        var lastBar = historicalData[lastIndex];

        // Fast path for no remaining trades
        if (activeOrders.Count > 0)
        {
            // Reuse sortedTrades collection for remaining trades
            sortedTrades.Clear();

            // Get remaining trades in order
            foreach (var kvp in activeOrders)
            {
                var trade = account.GetTradeById(kvp.Key);
                if (trade != null)
                {
                    sortedTrades.Add((kvp.Key, kvp.Value, trade));
                }
            }

            // Only sort if we have multiple trades
            if (sortedTrades.Count > 1)
            {
                // Sort by entry bar
                sortedTrades.Sort((a, b) => a.Trade!.EntryBar.CompareTo(b.Trade!.EntryBar));
            }

            // Close remaining trades
            foreach (var (tradeId, _, _) in sortedTrades)
            {
                account.ClosePosition(tradeId, lastBar.Close, lastIndex);
            }
        }

        var result = account.GenerateResult();

        // Don't terminate any strategies early - let them all complete for testing purposes
        result.TerminatedEarly = false;
        result.TerminationReason = string.Empty;

        return result;
    }
}
