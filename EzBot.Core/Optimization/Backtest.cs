using EzBot.Core.Strategy;
using EzBot.Models;

namespace EzBot.Core.Optimization;

public static class Backtest
{
    private const int WarmupPeriod = 10;
    private const int MinBarsForTermination = 50;

    public static BacktestResult Run(ITradingStrategy strategy, List<BarData> historicalData, BacktestOptions opt)
    {
        var results = Run([strategy], historicalData, opt);
        return results[0];
    }

    public static List<BacktestResult> Run(List<ITradingStrategy> strategies, List<BarData> historicalData, BacktestOptions opt)
    {
        // Skip some initial bars to allow indicators to initialize
        if (historicalData.Count <= WarmupPeriod)
            throw new ArgumentException("Not enough data for backtesting");

        // Initialize result storage
        var results = new List<BacktestResult>(strategies.Count);

        // Initialize tracking arrays for each strategy
        var accounts = new BacktestAccount[strategies.Count];
        var activeOrdersCollection = new Dictionary<int, TradeOrder>[strategies.Count];
        var lastTradeBarIndices = new int[strategies.Count];
        var inactiveDaysCounts = new int[strategies.Count];
        var continuousInactivityDays = new double[strategies.Count];
        var consecutiveLosses = new int[strategies.Count];
        var totalTrades = new int[strategies.Count];
        var peakBalances = new double[strategies.Count];

        // Initialize for each strategy
        for (int s = 0; s < strategies.Count; s++)
        {
            accounts[s] = new BacktestAccount(opt);
            activeOrdersCollection[s] = new Dictionary<int, TradeOrder>(opt.MaxConcurrentTrades);
            lastTradeBarIndices[s] = WarmupPeriod;
            peakBalances[s] = opt.InitialBalance;
            accounts[s].StartUnixTime = historicalData[WarmupPeriod].TimeStamp;
        }

        // Pre-allocate collections that are reused in the loop
        var sortedTradesCollection = new List<(int TradeId, TradeOrder Order, BacktestTrade? Trade)>[strategies.Count];
        var tradesClosedThisBarCollection = new List<int>[strategies.Count];

        for (int s = 0; s < strategies.Count; s++)
        {
            sortedTradesCollection[s] = new List<(int TradeId, TradeOrder Order, BacktestTrade? Trade)>(opt.MaxConcurrentTrades);
            tradesClosedThisBarCollection[s] = new List<int>(opt.MaxConcurrentTrades);
        }

        // Cache frequently used values
        int maxConcurrentTrades = opt.MaxConcurrentTrades;
        double maxDrawdown = opt.MaxDrawdown;
        int maxDaysInactive = opt.MaxDaysInactive;
        int historicalDataCount = historicalData.Count;
        int lastIndex = historicalDataCount - 1;
        double barsPerDay = 24.0 * 60.0 / (int)opt.TimeFrame;

        // Track which strategies are still active (haven't been terminated early)
        var activeStrategies = Enumerable.Range(0, strategies.Count).ToList();

        // Main backtest loop
        for (int i = WarmupPeriod; i < historicalDataCount; i++)
        {
            var currentBar = historicalData[i];

            // Process each active strategy
            for (int sIndex = activeStrategies.Count - 1; sIndex >= 0; sIndex--)
            {
                int s = activeStrategies[sIndex];
                var strategy = strategies[s];
                var account = accounts[s];
                var activeOrders = activeOrdersCollection[s];

                account.EndUnixTime = currentBar.TimeStamp;
                bool hadTradeActivity = false;

                // Clear collections
                var sortedTrades = sortedTradesCollection[s];
                var tradesClosedThisBar = tradesClosedThisBarCollection[s];
                sortedTrades.Clear();
                tradesClosedThisBar.Clear();

                // Process active trades
                if (activeOrders.Count > 0)
                {
                    // Get all active trades
                    foreach (var kvp in activeOrders)
                    {
                        var trade = account.GetTradeById(kvp.Key);
                        if (trade != null)
                        {
                            sortedTrades.Add((kvp.Key, kvp.Value, trade));
                        }
                    }

                    // Sort if needed
                    if (sortedTrades.Count > 1)
                    {
                        sortedTrades.Sort((a, b) => a.Trade!.EntryBar.CompareTo(b.Trade!.EntryBar));
                    }

                    // Process trades in order
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

                        // Check for stop loss and take profit
                        if (order.TradeType == TradeType.Long)
                        {
                            if (currentLow <= stopLoss)
                            {
                                account.ClosePosition(tradeId, stopLoss, i);
                                closed = true;
                                consecutiveLosses[s]++;
                                totalTrades[s]++;
                            }
                            else if (currentHigh >= takeProfit)
                            {
                                account.ClosePosition(tradeId, takeProfit, i);
                                closed = true;
                                consecutiveLosses[s] = 0;
                                totalTrades[s]++;
                            }
                        }
                        else if (order.TradeType == TradeType.Short)
                        {
                            if (currentHigh >= stopLoss)
                            {
                                account.ClosePosition(tradeId, stopLoss, i);
                                closed = true;
                                consecutiveLosses[s]++;
                                totalTrades[s]++;
                            }
                            else if (currentLow <= takeProfit)
                            {
                                account.ClosePosition(tradeId, takeProfit, i);
                                closed = true;
                                consecutiveLosses[s] = 0;
                                totalTrades[s]++;
                            }
                        }

                        if (closed)
                        {
                            tradesClosedThisBar.Add(tradeId);
                            hadTradeActivity = true;
                        }
                    }

                    // Remove closed trades
                    foreach (var tradeId in tradesClosedThisBar)
                    {
                        activeOrders.Remove(tradeId);
                    }
                }

                // Check for new positions
                int activeOrdersCount = activeOrders.Count;
                if (activeOrdersCount < maxConcurrentTrades)
                {
                    try
                    {
                        var tradeOrder = strategy.GetAction(historicalData, i);

                        if (tradeOrder.TradeType != TradeType.None)
                        {
                            int tradeId = account.OpenPosition(tradeOrder.TradeType,
                                currentBar.Close, tradeOrder.StopLoss, i);
                            activeOrders.Add(tradeId, tradeOrder);

                            lastTradeBarIndices[s] = i;
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
                    continuousInactivityDays[s] = 0;
                    lastTradeBarIndices[s] = i;
                }
                else if (activeOrders.Count > 0)
                {
                    continuousInactivityDays[s] = 0;
                    lastTradeBarIndices[s] = i;
                }
                else
                {
                    continuousInactivityDays[s] = (i - lastTradeBarIndices[s]) / barsPerDay;

                    int currentInactiveDays = (int)Math.Floor(continuousInactivityDays[s]);
                    if (currentInactiveDays > inactiveDaysCounts[s])
                        inactiveDaysCounts[s] = currentInactiveDays;
                }

                // Update account metrics
                account.MaxDaysInactive = inactiveDaysCounts[s];

                // Calculate current drawdown
                double currentBalance = account.CurrentBalance;
                if (currentBalance > peakBalances[s])
                    peakBalances[s] = currentBalance;

                double currentDrawdown = (peakBalances[s] - currentBalance) / peakBalances[s];
                account.UpdateMaxDrawdown(currentDrawdown);

                // Early termination check for obviously poor strategies
                if (i > MinBarsForTermination)
                {
                    string terminationReason = string.Empty;

                    // Progressive threshold
                    double progressFactor = Math.Min(1.0, (double)(i - MinBarsForTermination) / (historicalDataCount - MinBarsForTermination));
                    double adjustedMaxDrawdown = maxDrawdown * (1.5 - 0.5 * progressFactor);

                    // Calculate drawdown trend
                    int lookbackBars = Math.Min(50, i - MinBarsForTermination);
                    if (lookbackBars > 10 && currentDrawdown > adjustedMaxDrawdown * 0.7)
                    {
                        int previousBarIndex = i - lookbackBars;
                        double previousBalanceRatio = account.GetHistoricalBalance(previousBarIndex) / peakBalances[s];
                        double previousDrawdown = 1.0 - previousBalanceRatio;

                        if (currentDrawdown > adjustedMaxDrawdown && currentDrawdown > previousDrawdown * 1.05)
                        {
                            terminationReason = $"Worsening drawdown: {currentDrawdown:P2} > {adjustedMaxDrawdown:P2}, deteriorating trend";
                        }
                    }
                    else if (currentDrawdown > adjustedMaxDrawdown * 1.5)
                    {
                        terminationReason = $"Extreme drawdown: {currentDrawdown:P2} > {adjustedMaxDrawdown * 1.5:P2}";
                    }

                    // Inactivity check
                    double adjustedMaxInactivity = maxDaysInactive * (1.0 + 0.5 * (1.0 - progressFactor));
                    if (inactiveDaysCounts[s] > adjustedMaxInactivity)
                    {
                        terminationReason = $"Extended inactivity: {inactiveDaysCounts[s]} days > {adjustedMaxInactivity:F1} days";
                    }

                    if (!string.IsNullOrEmpty(terminationReason))
                    {
                        var earlyTerminationResult = account.GenerateResult();
                        earlyTerminationResult.TerminatedEarly = true;
                        earlyTerminationResult.TerminationReason = terminationReason;

                        results.Add(earlyTerminationResult);
                        activeStrategies.RemoveAt(sIndex);
                    }
                }
            }

            // If no strategies left active, we can exit early
            if (activeStrategies.Count == 0)
                break;
        }

        // Process remaining active strategies
        var lastBar = historicalData[lastIndex];

        foreach (int s in activeStrategies)
        {
            var account = accounts[s];
            var activeOrders = activeOrdersCollection[s];
            var sortedTrades = sortedTradesCollection[s];

            if (activeOrders.Count > 0)
            {
                sortedTrades.Clear();

                foreach (var kvp in activeOrders)
                {
                    var trade = account.GetTradeById(kvp.Key);
                    if (trade != null)
                    {
                        sortedTrades.Add((kvp.Key, kvp.Value, trade));
                    }
                }

                if (sortedTrades.Count > 1)
                {
                    sortedTrades.Sort((a, b) => a.Trade!.EntryBar.CompareTo(b.Trade!.EntryBar));
                }

                foreach (var (tradeId, _, _) in sortedTrades)
                {
                    account.ClosePosition(tradeId, lastBar.Close, lastIndex);
                }
            }

            var result = account.GenerateResult();
            result.TerminatedEarly = false;
            result.TerminationReason = string.Empty;

            results.Add(result);
        }

        return results;
    }
}
