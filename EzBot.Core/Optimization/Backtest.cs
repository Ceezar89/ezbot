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

        // Calculate total number of accounts (strategies × maxConcurrentTrades combinations × riskPercentage combinations)
        int totalAccounts = strategies.Count * opt.MaxConcurrentTrades.Count * opt.RiskPercentage.Count;

        // Initialize result storage
        var results = new List<BacktestResult>(totalAccounts);

        // Initialize tracking arrays for each strategy and maxConcurrentTrades combination
        var accounts = new BacktestAccount[totalAccounts];
        var activeOrdersCollection = new Dictionary<int, TradeOrder>[totalAccounts];
        var lastTradeBarIndices = new int[totalAccounts];
        var inactiveBarsCounts = new int[totalAccounts];
        var consecutiveLosses = new int[totalAccounts];
        var totalTrades = new int[totalAccounts];
        var peakBalances = new double[totalAccounts];
        var accountToStrategyMap = new int[totalAccounts]; // Maps account index to strategy index
        var accountToMaxTradesMap = new int[totalAccounts]; // Maps account index to maxConcurrentTrades index
        var accountToRiskMap = new int[totalAccounts]; // Maps account index to riskPercentage index

        // Initialize for each strategy and maxConcurrentTrades and riskPercentage combination
        int accountIndex = 0;
        for (int s = 0; s < strategies.Count; s++)
        {
            for (int m = 0; m < opt.MaxConcurrentTrades.Count; m++)
            {
                for (int r = 0; r < opt.RiskPercentage.Count; r++)
                {
                    accounts[accountIndex] = new BacktestAccount(opt, opt.RiskPercentage[r], opt.MaxConcurrentTrades[m]);
                    activeOrdersCollection[accountIndex] = new Dictionary<int, TradeOrder>(opt.MaxConcurrentTrades[m]);
                    lastTradeBarIndices[accountIndex] = WarmupPeriod;
                    peakBalances[accountIndex] = opt.InitialBalance;
                    accounts[accountIndex].StartUnixTime = historicalData[WarmupPeriod].TimeStamp;
                    accountToStrategyMap[accountIndex] = s;
                    accountToMaxTradesMap[accountIndex] = m;
                    accountToRiskMap[accountIndex] = r;
                    accountIndex++;
                }
            }
        }

        // Pre-allocate collections that are reused in the loop
        var sortedTradesCollection = new List<(int TradeId, TradeOrder Order, BacktestTrade? Trade)>[totalAccounts];
        var tradesClosedThisBarCollection = new List<int>[totalAccounts];

        for (int a = 0; a < totalAccounts; a++)
        {
            int maxTradesIndex = accountToMaxTradesMap[a];
            sortedTradesCollection[a] = new List<(int TradeId, TradeOrder Order, BacktestTrade? Trade)>(opt.MaxConcurrentTrades[maxTradesIndex]);
            tradesClosedThisBarCollection[a] = new List<int>(opt.MaxConcurrentTrades[maxTradesIndex]);
        }

        // Cache frequently used values
        double maxDrawdown = opt.MaxDrawdown;
        int historicalDataCount = historicalData.Count;
        int lastIndex = historicalDataCount - 1;

        // Calculate max inactivity threshold in bars rather than days
        int maxInactiveBars = (int)(historicalDataCount * opt.MaxInactivityPercentage);

        // Track which accounts are still active (haven't been terminated early)
        var activeAccounts = Enumerable.Range(0, totalAccounts).ToList();

        // Create a reusable buffer array/list outside the loop
        var dataBuffer = new List<BarData>(100);

        // Main backtest loop
        for (int i = WarmupPeriod; i < historicalDataCount; i++)
        {
            var currentBar = historicalData[i];

            // Process each active account
            for (int aIndex = activeAccounts.Count - 1; aIndex >= 0; aIndex--)
            {
                int a = activeAccounts[aIndex];
                int s = accountToStrategyMap[a];
                int maxTradesIndex = accountToMaxTradesMap[a];
                int maxConcurrentTrades = opt.MaxConcurrentTrades[maxTradesIndex];

                var strategy = strategies[s];
                var account = accounts[a];
                var activeOrders = activeOrdersCollection[a];

                account.EndUnixTime = currentBar.TimeStamp;
                bool hadTradeActivity = false;

                // Clear collections
                var sortedTrades = sortedTradesCollection[a];
                var tradesClosedThisBar = tradesClosedThisBarCollection[a];
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
                                consecutiveLosses[a]++;
                                totalTrades[a]++;
                            }
                            else if (currentHigh >= takeProfit)
                            {
                                account.ClosePosition(tradeId, takeProfit, i);
                                closed = true;
                                consecutiveLosses[a] = 0;
                                totalTrades[a]++;
                            }
                        }
                        else if (order.TradeType == TradeType.Short)
                        {
                            if (currentHigh >= stopLoss)
                            {
                                account.ClosePosition(tradeId, stopLoss, i);
                                closed = true;
                                consecutiveLosses[a]++;
                                totalTrades[a]++;
                            }
                            else if (currentLow <= takeProfit)
                            {
                                account.ClosePosition(tradeId, takeProfit, i);
                                closed = true;
                                consecutiveLosses[a] = 0;
                                totalTrades[a]++;
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
                        // Clear and refill buffer
                        dataBuffer.Clear();
                        int startIdx = Math.Max(0, i + 1 - 100);
                        int count = i + 1 - startIdx;

                        for (int bufIdx = 0; bufIdx < count; bufIdx++)
                        {
                            dataBuffer.Add(historicalData[startIdx + bufIdx]);
                        }

                        var tradeOrder = strategy.GetAction(dataBuffer);

                        if (tradeOrder.TradeType != TradeType.None)
                        {
                            int tradeId = account.OpenPosition(tradeOrder.TradeType,
                                currentBar.Close, tradeOrder.StopLoss, i);
                            activeOrders.Add(tradeId, tradeOrder);

                            lastTradeBarIndices[a] = i;
                            hadTradeActivity = true;
                        }
                    }
                    catch (Exception)
                    {
                        // Skip problematic trade signals rather than failing the entire backtest
                        continue;
                    }
                }

                // Update inactivity tracking - now in bars instead of days
                if (hadTradeActivity)
                {
                    inactiveBarsCounts[a] = 0;
                    lastTradeBarIndices[a] = i;
                }
                else if (activeOrders.Count > 0)
                {
                    inactiveBarsCounts[a] = 0;
                    lastTradeBarIndices[a] = i;
                }
                else
                {
                    inactiveBarsCounts[a] = i - lastTradeBarIndices[a];
                }

                // Update account metrics
                account.MaxDaysInactive = inactiveBarsCounts[a]; // This will store bars now, not days

                // Calculate current drawdown
                double currentBalance = account.CurrentBalance;
                if (currentBalance > peakBalances[a])
                    peakBalances[a] = currentBalance;

                double currentDrawdown = (peakBalances[a] - currentBalance) / peakBalances[a];
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
                        double previousBalanceRatio = account.GetHistoricalBalance(previousBarIndex) / peakBalances[a];
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

                    double adjustedMaxInactivityBars = maxInactiveBars * (1.0 + 0.5 * (1.0 - progressFactor));
                    if (inactiveBarsCounts[a] > adjustedMaxInactivityBars)
                    {
                        double inactivityPercentage = (double)inactiveBarsCounts[a] / i * 100;
                        terminationReason = $"Extended inactivity: {inactiveBarsCounts[a]} bars ({inactivityPercentage:F1}%) > {adjustedMaxInactivityBars:F1} bars";
                    }

                    if (!string.IsNullOrEmpty(terminationReason))
                    {
                        var earlyTerminationResult = account.GenerateResult();
                        earlyTerminationResult.TerminatedEarly = true;
                        earlyTerminationResult.TerminationReason = terminationReason;

                        results.Add(earlyTerminationResult);
                        activeAccounts.RemoveAt(aIndex);
                    }
                }
            }

            // If no accounts left active, we can exit early
            if (activeAccounts.Count == 0)
                break;
        }

        // Process remaining active accounts
        var lastBar = historicalData[lastIndex];

        foreach (int a in activeAccounts)
        {
            var account = accounts[a];
            var activeOrders = activeOrdersCollection[a];
            var sortedTrades = sortedTradesCollection[a];

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
