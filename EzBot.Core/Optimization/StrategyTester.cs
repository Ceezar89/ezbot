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
    private readonly ConcurrentBag<(IndicatorCollection Params, BacktestResult Result)> results = [];
    private readonly Stopwatch totalStopwatch = new();
    private long currentCombinationIndex = 0;
    private int terminatedEarlyCount = 0;
    // Timestamp for periodic saving
    private DateTime lastSaveTime = DateTime.Now;
    private readonly TimeSpan saveInterval = TimeSpan.FromSeconds(120);

    // Add dictionary to track termination reasons
    private readonly ConcurrentDictionary<string, int> terminationReasons = new();

    // Progress display
    private readonly ManualResetEventSlim stopRequested = new(false);
    private bool isRunning = false;

    // Total number of parameter combinations to test
    private readonly long totalCombinations;

    // Batching for improved cache locality
    private const int BatchSize = 100;

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
        int maxDaysInactive = 14
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
            maxDaysInactive
        );

        // Load and prepare historical data - do this once upfront
        historicalData = LoadAndPrepareData(dataFilePath, lookbackDays, timeFrame);

        // Calculate total number of parameter combinations to test
        var templateCollection = new IndicatorCollection(strategyType);

        // Print debug information about individual indicators
        foreach (var indicator in templateCollection)
        {
            int permutations = indicator.GetParameters().GetPermutationCount();
        }

        // Calculate theoretical total and verify with actual iteration count
        int theoreticalTotal = templateCollection.GetTotalParameterPermutations();

        // Print the initial parameter state

        // Print the first few iterations to check parameter changes
        int iterations = 0;
        var testCollection = templateCollection.DeepClone();
        testCollection.ResetIteration();

        while (iterations < 5 && testCollection.Next())
        {
            iterations++;
        }

        // Directly count actual permutations (can be slow for large numbers)
        int actualCount = templateCollection.GetTotalParameterPermutations();

        // Check if counts match
        if (theoreticalTotal != actualCount)
        {
            Console.WriteLine("\nWARNING: Mismatch between theoretical and actual parameter combinations!");
            Console.WriteLine("This indicates an issue with parameter ranges, steps, or increment logic.");
            Console.WriteLine($"Theoretical: {theoreticalTotal:N0}, Actual: {actualCount:N0}");

            // Analyze each indicator to identify which ones have discrepancies
            Console.WriteLine("\nAnalyzing indicators for discrepancies:");
            foreach (var indicator in templateCollection)
            {
                var indType = indicator.GetType().Name;
                var indParams = indicator.GetParameters();
                var indPermCount = indParams.GetPermutationCount();

                // Count actual permutations for this single indicator
                var singleIndCollection = new IndicatorCollection();
                singleIndCollection.Add((IIndicator)Activator.CreateInstance(indicator.GetType(), indParams.DeepClone())!);
                int actualIndCount = singleIndCollection.GetTotalParameterPermutations();

                string match = indPermCount == actualIndCount ? "✓" : "✗";
                Console.WriteLine($"- {indType}: Theoretical {indPermCount}, Actual {actualIndCount} {match}");

                if (indPermCount != actualIndCount)
                {
                    // Print parameter details for mismatched indicators
                    var props = indParams.GetProperties();
                    foreach (var prop in props)
                    {
                        Console.WriteLine($"  • {prop.Name}: Min={prop.Min}, Max={prop.Max}, Step={prop.Step}, CurrentValue={prop.Value}");
                    }
                }
            }
        }

        // Use the actual count for processing
        totalCombinations = actualCount;
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
        Console.WriteLine($"- Total Combinations: {totalCombinations:N0}");

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
    /// Worker thread that processes parameter combinations in batches for better cache locality
    /// </summary>
    private void ProcessParameters()
    {
        // Each worker thread will use its own indicator collection
        var parameters = new IndicatorCollection(strategyType);

        while (true)
        {
            // Get the next batch of combinations to process
            long batchStart = Interlocked.Add(ref currentCombinationIndex, BatchSize) - BatchSize;

            // Check if we've processed all combinations
            if (batchStart >= totalCombinations)
                break;

            // Calculate actual batch size (may be smaller for the last batch)
            int actualBatchSize = (int)Math.Min(BatchSize, totalCombinations - batchStart);

            // Reset parameters to starting values
            parameters.ResetIteration();

            try
            {
                // Skip to the batch start position
                for (long i = 0; i < batchStart; i++)
                {
                    if (!parameters.Next())
                    {
                        // We've reached the end unexpectedly
                        // This should not happen if totalCombinations is correct
                        return;
                    }
                }

                // Process each combination in the batch
                for (int i = 0; i < actualBatchSize; i++)
                {
                    try
                    {
                        // Create strategy with these parameters
                        var strategy = StrategyFactory.CreateStrategy(strategyType, parameters.DeepClone());

                        // Run backtest with the current parameters
                        var backtestResult = Backtest.Run(strategy, historicalData, backtestOptions);

                        if (backtestResult.TotalTrades == 0)
                        {
                            continue;
                        }
                        else
                        {
                            // Add successful result to collection
                            results.Add((parameters.DeepClone(), backtestResult));
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Exception during backtest: {ex.Message}");
                    }

                    // Move to the next parameter combination if not done with batch
                    if (i < actualBatchSize - 1 && !parameters.Next())
                    {
                        // We've reached the end of all combinations
                        break;
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
    /// Updates the progress display at fixed intervals
    /// </summary>
    private void UpdateProgressDisplay()
    {
        int progressBarWidth = 50;
        DateTime lastUpdate = DateTime.Now;
        long lastCount = 0;
        double combinationsPerSecond = 0;

        // Cache for string building to reduce allocations
        char[] progressBar = new char[progressBarWidth + 2]; // +2 for brackets
        progressBar[0] = '[';
        progressBar[progressBarWidth + 1] = ']';

        while (!stopRequested.IsSet)
        {
            long current = Interlocked.Read(ref currentCombinationIndex);
            int resultsCount = results.Count;
            int terminatedCount = terminatedEarlyCount;

            // Calculate combinations per second
            var elapsed = DateTime.Now - lastUpdate;
            if (elapsed.TotalSeconds >= 1)
            {
                combinationsPerSecond = (current - lastCount) / elapsed.TotalSeconds;
                lastCount = current;
                lastUpdate = DateTime.Now;
            }

            // Check if it's time to trim results and save
            var timeSinceLastSave = DateTime.Now - lastSaveTime;
            if (timeSinceLastSave >= saveInterval && !results.IsEmpty)
            {
                TrimResultsToTop100();
                SaveTopResults(100);
                lastSaveTime = DateTime.Now;
                Console.WriteLine($"\nSaved top 100 results at {DateTime.Now.ToShortTimeString()}");
            }

            // Calculate completion percentage
            int percentage = (int)((double)current / totalCombinations * 100);
            int filledWidth = (int)(progressBarWidth * percentage / 100.0);

            // Fill progress bar
            for (int i = 0; i < progressBarWidth; i++)
            {
                progressBar[i + 1] = i < filledWidth ? '◼' : ' ';
            }

            // Format status line with string interpolation (more efficient than multiple concatenations)
            string statusLine = $"\r{new string(progressBar)} {percentage}% ( {current:N0} / {totalCombinations:N0} ) " +
                               $"[{combinationsPerSecond:N0}/s] Found: {resultsCount:N0}  ";

            // Write to console without a newline
            Console.Write(statusLine);

            // Wait for next update
            Thread.Sleep(500);
        }

        Console.WriteLine();  // End the progress line
    }

    /// <summary>
    /// Trims the results collection to keep only the top 100 items by net profit
    /// </summary>
    private void TrimResultsToTop100()
    {
        // Skip trimming if we have fewer than 100 results
        if (results.Count <= 100)
            return;

        // Extract all results, sort, and keep top 100
        var resultArray = results.ToArray();
        var top100 = resultArray.OrderByDescending(r => r.Result.NetProfit).Take(100).ToArray();

        // Clear the bag and add back only the top 100
        results.Clear();
        foreach (var result in top100)
        {
            results.Add(result);
        }
    }

    /// <summary>
    /// Saves top N results to a JSON file
    /// </summary>
    private void SaveTopResults(int count)
    {
        try
        {
            // Use ToArray for better performance on ConcurrentBag
            var resultArray = results.ToArray();
            var topResults = resultArray.OrderByDescending(r => r.Result.NetProfit).Take(count).ToList();

            if (topResults.Count == 0)
                return;

            // Create a list of test results
            var testResults = topResults.Select(r => new TestResult(r)).ToList();

            // Create a unique filename based on timestamp
            string fileName = $"top_{strategyType}_{DateTime.Now:yyyyMMdd_HHmmss}.json";

            // Serialize with indentation for readability
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            string json = JsonSerializer.Serialize(testResults, options);
            File.WriteAllText(fileName, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError saving top results: {ex.Message}");
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
        Console.WriteLine($"Successful: {results.Count:N0} strategies");
        Console.WriteLine($"Terminated: {terminatedEarlyCount:N0} strategies");
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

        if (results.IsEmpty)
        {
            Console.WriteLine("\nNo successful optimization results found.");
            return;
        }

        // Use ToArray for better performance on ConcurrentBag
        var resultArray = results.ToArray();
        var topResults = resultArray.OrderByDescending(r => r.Result.NetProfit).Take(10).ToList();

        Console.WriteLine("\nTop 10 Results:");
        Console.WriteLine("===============");

        for (int i = 0; i < topResults.Count; i++)
        {
            var result = topResults[i];
            Console.WriteLine($"{i + 1}. Net Profit: ${result.Result.NetProfit:F2}, " +
                             $"Win Rate: {result.Result.WinRate:P2}, " +
                             $"Trades: {result.Result.TotalTrades}");
        }

        // Save the top result to a file
        if (topResults.Count > 0)
        {
            var bestResult = topResults[0];
            SaveBestResult(bestResult);
        }
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

            // Create a unique filename based on the strategy and statistics
            string fileName = $"best_{strategyType}_{bestResult.Result.NetProfit:F0}_{bestResult.Result.WinRate:P0}.json";

            // Serialize with indentation for readability
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            string json = JsonSerializer.Serialize(testResult, options);
            File.WriteAllText(fileName, json);

            Console.WriteLine($"\nSaved best result to {fileName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError saving best result: {ex.Message}");
        }
    }
}

