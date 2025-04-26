using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using EzBot.Common;
using EzBot.Core.Factory;
using EzBot.Core.Indicator;
using EzBot.Core.Strategy;
using EzBot.Models;

namespace EzBot.Core.Optimization;

/// <summary>
/// Tests trading strategies using brute-force parameter optimization.
/// Generates parameter combinations sequentially and distributes them to worker threads.
/// </summary>
public class StrategyTester
{
    // Core configuration
    private readonly StrategyType strategyType;
    private readonly BacktestOptions backtestOptions;
    private readonly List<BarData> historicalData = [];
    private readonly int threadCount;

    // State tracking with improved concurrency
    // Use dictionary to track results with unique parameter configurations
    private readonly ConcurrentDictionary<string, (IndicatorCollection Params, BacktestResult Result)> Results = new();
    // Shared parameter cache to prevent duplicate backtesting across threads
    private readonly ConcurrentDictionary<string, bool> processedParameters = new();
    private readonly ConcurrentBag<IndicatorCollection> allPermutations = [];
    private readonly Stopwatch totalStopwatch = new();
    private long currentCombinationIndex = 0;
    private int terminatedEarlyCount = 0;
    private int successfulCount = 0;
    private int failedCount = 0;
    private int duplicateCount = 0;
    private DateTime lastSaveTime = DateTime.Now;
    private readonly TimeSpan saveInterval = TimeSpan.FromSeconds(60);

    // Add dictionary to track termination reasons
    private readonly ConcurrentDictionary<string, int> terminationReasons = new();

    // Progress display
    private readonly ManualResetEventSlim stopRequested = new(false);
    private bool isRunning = false;

    // Total number of parameter combinations to test
    private readonly long totalCombinations;

    // Batching for improved cache locality
    private const int BatchSize = 30;

    public StrategyTester(
        string dataFilePath,
        StrategyType strategyType,
        TimeFrame timeFrame = TimeFrame.OneHour,
        double initialBalance = 1000,
        double feePercentage = 0.05,
        int maxConcurrentTrades = 5,
        int leverage = 10,
        int lookbackDays = 1500,
        int threadCount = -1,
        double maxDrawdown = 0.5,
        int maxDaysInactive = 14,
        double riskPercentage = 1.0
    )
    {
        this.strategyType = strategyType;

        // Configure thread count (default to CPU cores - 1)
        this.threadCount = threadCount == -1
            ? Math.Max(1, Environment.ProcessorCount - 1)
            : (threadCount == 0 ? Environment.ProcessorCount : threadCount);

        // Cap thread count to available CPU cores
        if (this.threadCount > Environment.ProcessorCount)
            this.threadCount = Environment.ProcessorCount;

        // Initialize backtest options
        backtestOptions = new(
            initialBalance,
            feePercentage,
            leverage,
            timeFrame,
            lookbackDays,
            maxDrawdown,
            maxConcurrentTrades,
            maxDaysInactive,
            riskPercentage
        );

        // Load and prepare historical data - do this once upfront
        historicalData = LoadAndPrepareData(dataFilePath, lookbackDays, timeFrame);

        Console.WriteLine($"Generating all permutations for {strategyType}");

        allPermutations = [.. IndicatorCollection.GenerateAllPermutations(strategyType)];
        totalCombinations = allPermutations.Count;

        Console.WriteLine();

        // Validate that we have a reasonable number of combinations
        if (totalCombinations <= 0)
        {
            throw new InvalidOperationException("No valid parameter combinations found for this strategy type.");
        }
    }


    /// <summary>
    /// Loads and prepares the historical data - extracted to a method to improve readability
    /// </summary>
    private static List<BarData> LoadAndPrepareData(string dataFilePath, int lookbackDays, TimeFrame timeFrame)
    {
        // Load historical data
        var data = CsvDataUtility.LoadBarDataFromCsv(dataFilePath);

        // Calculate number of bars needed
        int barsNeeded = lookbackDays * 24 * 60;

        // Trim data to lookback period
        if (data.Count > barsNeeded)
            data = [.. data.Skip(data.Count - barsNeeded)];

        // Convert to desired timeframe
        return TimeFrameUtility.ConvertTimeFrame(data, timeFrame);
    }

    /// <summary>
    /// Start testing all parameter combinations across multiple threads
    /// </summary>
    public void Test()
    {
        if (isRunning)
            return;

        isRunning = true;
        totalStopwatch.Restart();

        Console.WriteLine($"- Strategy Type: {strategyType}");
        Console.WriteLine($"- Worker Thread Count: {threadCount}");
        Console.WriteLine($"- Data Points: {historicalData.Count:N0}");
        Console.WriteLine($"- Time Frame: {backtestOptions.TimeFrame}");
        Console.WriteLine($"- Max Concurrent Trades: {backtestOptions.MaxConcurrentTrades}");
        Console.WriteLine($"- Max Drawdown: {backtestOptions.MaxDrawdown * 100:F0}%");
        Console.WriteLine($"- Max Days Inactive: {backtestOptions.MaxDaysInactive} days");
        Console.WriteLine($"- Lookback Window: {backtestOptions.LookbackDays} days");
        Console.WriteLine($"- Initial Balance: ${backtestOptions.InitialBalance:F2}");
        Console.WriteLine($"- Fee Percentage: {backtestOptions.FeePercentage:F2}%");
        Console.WriteLine($"- Leverage: {backtestOptions.Leverage}x");
        Console.WriteLine($"- Risk Percentage: {backtestOptions.RiskPercentage:F2}%");
        Console.WriteLine($"- Total Combinations: {totalCombinations:N0}");
        Console.WriteLine($"- Batch Size: {BatchSize} strategies per backtest");

        // Start UI thread for progress display
        var uiThread = new Thread(UpdateProgressDisplay)
        {
            IsBackground = true,
            Name = "ProgressThread"
        };
        uiThread.Start();

        // Create and start worker threads
        var workers = new Thread[threadCount];
        for (int i = 0; i < threadCount; i++)
        {
            workers[i] = new Thread(ProcessParameters)
            {
                IsBackground = true,
                Name = $"Worker-{i}"
            };
            workers[i].Start();
        }

        // Wait for all worker threads to complete
        foreach (var worker in workers)
        {
            worker.Join();
        }

        // Stop the UI thread
        stopRequested.Set();
        uiThread.Join();

        totalStopwatch.Stop();
        isRunning = false;

        // Show final results
        ShowFinalStats();
    }

    /// <summary>
    /// Worker thread that processes parameter combinations in batches for better performance
    /// </summary>
    private void ProcessParameters()
    {
        while (true)
        {
            try
            {
                List<IndicatorCollection> parameterInstances = [];
                // try take a bachsize amount of parameters from the allPermutations list
                int index = 0;
                while (index < BatchSize && index < allPermutations.Count)
                {
                    if (allPermutations.TryTake(out var parameter))
                    {
                        parameterInstances.Add(parameter);
                        index++;
                    }
                    else break;
                }

                if (parameterInstances.Count == 0)
                    break;

                int actualBatchSize = parameterInstances.Count;

                // Create batch collections
                var strategies = new List<ITradingStrategy>(actualBatchSize);
                var parameterKeys = new List<string>(actualBatchSize);

                foreach (var parameter in parameterInstances)
                {
                    string paramKey = IndicatorCollection.GenerateParameterKey(parameter);
                    if (processedParameters.TryAdd(paramKey, true))
                    {
                        var strategy = StrategyFactory.CreateStrategy(strategyType, parameter);
                        strategies.Add(strategy);
                        parameterKeys.Add(paramKey);
                    }
                }

                // Run backtest for all strategies in the batch at once if we have any
                if (strategies.Count > 0)
                {
                    try
                    {
                        var backtestResults = Backtest.Run(strategies, historicalData, backtestOptions);

                        // Process results from all strategies
                        for (int i = 0; i < backtestResults.Count; i++)
                        {
                            var result = backtestResults[i];
                            var paramKey = parameterKeys[i];

                            // Track terminated strategies
                            if (result.TerminatedEarly)
                            {
                                Interlocked.Increment(ref terminatedEarlyCount);

                                // Record termination reason
                                if (!string.IsNullOrEmpty(result.TerminationReason))
                                {
                                    terminationReasons.AddOrUpdate(
                                        result.TerminationReason,
                                        1,
                                        (_, count) => count + 1
                                    );
                                }
                            }
                            // Add successful strategies to results
                            else if (result.NetProfit > 0 && result.WinRate > 0.5)
                            {
                                Interlocked.Increment(ref successfulCount);
                                if (!Results.TryAdd(paramKey, (parameterInstances[i], result)))
                                {
                                    // It's a duplicate - check if the new result is better
                                    if (result.NetProfit > Results[paramKey].Result.NetProfit)
                                    {
                                        // Replace with better result
                                        Results[paramKey] = (parameterInstances[i], result);
                                    }
                                    Interlocked.Increment(ref duplicateCount);
                                }
                            }
                            else
                            {
                                Interlocked.Increment(ref failedCount);
                            }
                        }

                        // Increment the processed combinations counter
                        Interlocked.Add(ref currentCombinationIndex, backtestResults.Count);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Exception during batch backtest: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in worker thread: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Trims the results collection to keep only the top 10 items by net profit
    /// </summary>
    private void TrimResultsToTop10()
    {
        // Skip trimming if we have fewer than 10 results
        if (Results.Count <= 10)
            return;

        // Extract all values, sort, and keep top 10
        var resultArray = Results.Values.ToArray();

        // Group by performance metrics to eliminate duplicates with identical results
        var groupedResults = resultArray
            .GroupBy(r => new
            {
                NetProfit = Math.Round(r.Result.NetProfit, 2),
                WinRate = Math.Round(r.Result.WinRate, 4),
                TotalTrades = r.Result.TotalTrades
            })
            .Select(g => g.First()) // Take only the first result from each group
            .OrderByDescending(r => r.Result.NetProfit)
            .Take(10)
            .ToArray();

        // Create new dictionary with just the deduplicated top 10
        var newResults = new ConcurrentDictionary<string, (IndicatorCollection Params, BacktestResult Result)>();

        foreach (var result in groupedResults)
        {
            var key = IndicatorCollection.GenerateParameterKey(result.Params);
            newResults.TryAdd(key, result);
        }

        // Replace the old dictionary with the new one
        Results.Clear();
        foreach (var kvp in newResults)
        {
            Results.TryAdd(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Generates a filename for saving results based on strategy, timeframe, risk, and concurrent trades
    /// </summary>
    private string GenerateResultFilename()
    {
        // Format: StrategyType_LookbackDays_TimeFrame_RiskPercentage_MaxConcurrentTrades.json
        string fileName = $"{strategyType}_{backtestOptions.LookbackDays}days_{backtestOptions.TimeFrame}_{backtestOptions.RiskPercentage}R_{backtestOptions.MaxConcurrentTrades}C.json";
        return fileName;
    }

    /// <summary>
    /// Saves top 10 results to a JSON file
    /// </summary>
    private void SaveTopResults()
    {
        try
        {
            // Get values, sort, and take top
            var resultArray = Results.Values.ToArray();

            // Deduplicate results based on performance metrics
            var currentResults = resultArray
                .GroupBy(r => new
                {
                    NetProfit = Math.Round(r.Result.NetProfit, 2),
                    WinRate = Math.Round(r.Result.WinRate, 4),
                    TotalTrades = r.Result.TotalTrades
                })
                .Select(g => g.First()) // Take only the first result from each group
                .OrderByDescending(r => r.Result.NetProfit)
                .ToList();

            if (currentResults.Count == 0)
                return;

            // Create a list of test results from current results
            var testResults = currentResults.Select(r => new TestResult(r)).ToList();

            // Generate filename with strategy, lookback days, timeframe, risk, and concurrent trades
            string fileName = GenerateResultFilename();

            // Check if file exists and merge results
            if (File.Exists(fileName))
            {
                try
                {
                    // Load existing file
                    string existingJson = File.ReadAllText(fileName);
                    var jsonOptions = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    var existingResults = JsonSerializer.Deserialize<List<TestResult>>(existingJson, jsonOptions);

                    // If we have existing results, merge them with the current results
                    if (existingResults != null && existingResults.Count > 0)
                    {
                        // Combine existing and new results
                        testResults.AddRange(existingResults);

                        // Deduplicate the combined results based on performance metrics
                        testResults = testResults
                            .GroupBy(r => new
                            {
                                NetProfit = Math.Round(r.Result.NetProfit, 2),
                                WinRate = Math.Round(r.Result.WinRatePercent, 2),
                                TotalTrades = r.Result.TotalTrades
                            })
                            .Select(g => g.First())
                            .OrderByDescending(r => r.Result.NetProfit)
                            .Take(10)
                            .ToList();
                    }
                }
                catch (Exception ex)
                {
                    // If there's an error reading the file, log it and continue with current results
                    Console.WriteLine($"\nError reading existing file: {ex.Message}. Will create new file with current results.");
                }
            }

            // Ensure we only save the top 10
            if (testResults.Count > 10)
                testResults = testResults.Take(10).ToList();

            // Always save the results - we've merged with existing if necessary
            var serializeOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            string json = JsonSerializer.Serialize(testResults, serializeOptions);
            File.WriteAllText(fileName, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError saving top results: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates the progress display at fixed intervals
    /// </summary>
    private void UpdateProgressDisplay()
    {
        int progressBarWidth = 50;

        // Cache for string building to reduce allocations
        char[] progressBar = new char[progressBarWidth + 2]; // +2 for brackets
        progressBar[0] = '[';
        progressBar[progressBarWidth + 1] = ']';

        while (!stopRequested.IsSet)
        {
            long current = Interlocked.Read(ref currentCombinationIndex);

            // Check if it's time to trim results and save
            var timeSinceLastSave = DateTime.Now - lastSaveTime;
            if (timeSinceLastSave >= saveInterval && Results.Count > 0)
            {
                TrimResultsToTop10();
                SaveTopResults();
                lastSaveTime = DateTime.Now;
            }

            // Calculate completion percentage
            int percentage = (int)((double)current / totalCombinations * 100);
            int filledWidth = (int)(progressBarWidth * percentage / 100.0);

            // Fill progress bar
            for (int i = 0; i < progressBarWidth; i++)
            {
                progressBar[i + 1] = i < filledWidth ? '◼' : ' ';
            }

            string statusLine = $"\r{new string(progressBar)} {percentage}% ({current:N0}/{totalCombinations:N0}) " + $"Found: {Results.Count:N0}  ";

            // Write to console without a newline
            Console.Write(statusLine);

            // Wait for next update
            Thread.Sleep(500);
        }

        Console.WriteLine();  // End the progress line
    }

    /// <summary>
    /// Save the best result to a file
    /// </summary>
    private void SaveBestResult((IndicatorCollection Params, BacktestResult Result) bestResult)
    {
        try
        {
            // Create a JSON-friendly result
            var testResult = new TestResult(bestResult);

            // Generate filename with strategy, lookback days, timeframe, risk, and concurrent trades
            string fileName = GenerateResultFilename();

            // Check if file exists and compare with new result
            bool shouldSave = true;
            if (File.Exists(fileName))
            {
                try
                {
                    // Load existing file
                    string existingJson = File.ReadAllText(fileName);
                    var deserializeOptions = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    // Try to parse as list first (since we're now always saving as a list)
                    List<TestResult>? existingResults = null;

                    try
                    {
                        existingResults = JsonSerializer.Deserialize<List<TestResult>>(existingJson, deserializeOptions);

                        // If parsed as list, compare with best result
                        if (existingResults != null && existingResults.Count > 0)
                        {
                            double existingBestProfit = existingResults.Max(r => r.Result.NetProfit);
                            double newProfit = bestResult.Result.NetProfit;

                            // Check if new result would make it into the top 10
                            if (existingResults.Count < 10 || newProfit > existingResults.Min(r => r.Result.NetProfit))
                            {
                                // Add the new result and keep only top 10
                                existingResults.Add(testResult);

                                // Deduplicate and sort
                                existingResults = existingResults
                                    .GroupBy(r => new
                                    {
                                        NetProfit = Math.Round(r.Result.NetProfit, 2),
                                        WinRate = Math.Round(r.Result.WinRatePercent, 2),
                                        TotalTrades = r.Result.TotalTrades
                                    })
                                    .Select(g => g.First())
                                    .OrderByDescending(r => r.Result.NetProfit)
                                    .Take(10)
                                    .ToList();

                                // Save the updated list
                                var saveOptions = new JsonSerializerOptions
                                {
                                    WriteIndented = true,
                                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                                };

                                string json = JsonSerializer.Serialize(existingResults, saveOptions);
                                File.WriteAllText(fileName, json);

                                // We've already saved, so don't save again
                                shouldSave = false;
                            }
                            else
                            {
                                shouldSave = false;
                            }
                        }
                    }
                    catch
                    {
                        // If parsing as list fails, continue with shouldSave = true
                        Console.WriteLine("\nExisting file format is not compatible. Will create new file.");
                    }
                }
                catch (Exception ex)
                {
                    // If there's an error reading the file, proceed with saving
                    Console.WriteLine($"\nError reading existing file: {ex.Message}. Will overwrite with new result.");
                    shouldSave = true;
                }
            }

            if (shouldSave)
            {
                // Create a list with the single result
                var resultsList = new List<TestResult> { testResult };

                // Serialize with indentation for readability
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                string json = JsonSerializer.Serialize(resultsList, options);
                File.WriteAllText(fileName, json);

                Console.WriteLine($"\nSaved best result to {fileName}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError saving best result: {ex.Message}");
        }
    }

    /// <summary>
    /// Display final statistics and top results
    /// </summary>
    private void ShowFinalStats()
    {
        Console.WriteLine("\nOptimization Complete!");
        Console.WriteLine("=====================");
        Console.WriteLine($"Tested: {currentCombinationIndex:N0} combinations");
        Console.WriteLine($"Terminated: {terminatedEarlyCount:N0} strategies");
        Console.WriteLine($"Successful: {successfulCount:N0} unique strategies");
        Console.WriteLine($"Failed: {failedCount:N0} unique strategies");
        Console.WriteLine($"Results Count: {Results.Count:N0} unique strategies");
        Console.WriteLine($"Duplicate: {duplicateCount:N0} strategies");
        Console.WriteLine($"Processed: {processedParameters.Count:N0} unique strategies");
        Console.WriteLine($"Total time: {totalStopwatch.Elapsed}");

        // Display termination reasons if there are any
        if (terminationReasons.Count > 0)
        {
            Console.WriteLine("\nTermination Reasons:");
            Console.WriteLine("===================");
            foreach (var reason in terminationReasons.OrderByDescending(r => r.Value))
            {
                Console.WriteLine($"- {reason.Key}: {reason.Value:N0} strategies ({reason.Value * 100.0 / terminatedEarlyCount:F1}%)");
            }
        }

        if (Results.Count == 0)
        {
            Console.WriteLine("\nNo successful optimization results found.");
            return;
        }

        // Deduplicate by performance metrics before displaying results
        var topResults = Results.Values
            .GroupBy(r => new
            {
                NetProfit = Math.Round(r.Result.NetProfit, 2),
                WinRate = Math.Round(r.Result.WinRate, 4),
                TotalTrades = r.Result.TotalTrades
            })
            .Select(g => g.First())
            .OrderByDescending(r => r.Result.NetProfit)
            .Take(10)
            .ToList();

        Console.WriteLine("\nTop 10 Results:");
        Console.WriteLine("===============");

        for (int i = 0; i < topResults.Count; i++)
        {
            var result = topResults[i];
            Console.WriteLine($"{i + 1}. Net Profit: ${result.Result.NetProfit:F2}, " +
                             $"Win Rate: {result.Result.WinRate:P2}, " +
                             $"Trades: {result.Result.TotalTrades}");
        }

        // Save the top results to a file
        if (topResults.Count > 0)
        {
            // First save just the best result
            var bestResult = topResults[0];
            SaveBestResult(bestResult);

            // Then save all top 10 results
            SaveTopResults();
        }
    }
}

