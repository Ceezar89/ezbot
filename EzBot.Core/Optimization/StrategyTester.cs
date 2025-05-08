using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using EzBot.Common;
using EzBot.Core.Strategy;
using EzBot.Core.Indicator;
using EzBot.Models;
using System.Text.Json.Serialization;

namespace EzBot.Core.Optimization;

public class StrategyTester
{
    // Core configuration
    private readonly string strategyName;
    private readonly StrategyConfiguration strategyConfiguration;
    private readonly BacktestOptions backtestOptions;
    private readonly List<BarData> historicalData = [];
    private readonly int threadCount;
    private readonly bool runSavedConfiguration;
    private readonly double maxInactivityPercentage;

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
    private readonly int totalVariationsPerStrategy;

    // Batching for improved cache locality
    private const int BatchSize = 30;

    public StrategyTester(
        string dataFilePath,
        StrategyConfiguration strategyConfiguration,
        TimeFrame timeFrame = TimeFrame.OneHour,
        double initialBalance = 1000,
        double feePercentage = 0.05,
        List<int>? maxConcurrentTrades = null,
        int leverage = 10,
        int lookbackDays = 1500,
        int threadCount = -1,
        double maxDrawdown = 0.5,
        List<double>? riskPercentage = null,
        double maxInactivityPercentage = 0.05,
        bool runSavedConfiguration = false
    )
    {
        this.strategyConfiguration = strategyConfiguration;
        this.runSavedConfiguration = runSavedConfiguration;
        this.maxInactivityPercentage = maxInactivityPercentage;
        strategyName = strategyConfiguration.Name;

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
            maxConcurrentTrades ?? [1],
            riskPercentage ?? [1.0],
            maxInactivityPercentage
        );

        // Calculate total variations per strategy
        totalVariationsPerStrategy = backtestOptions.MaxConcurrentTrades.Count * backtestOptions.RiskPercentage.Count;

        // Load and prepare historical data - do this once upfront
        historicalData = LoadAndPrepareData(dataFilePath, lookbackDays, timeFrame);

        if (strategyConfiguration == null)
        {
            throw new ArgumentNullException(nameof(strategyConfiguration), "Strategy configuration cannot be null.");
        }

        allPermutations = [.. IndicatorCollection.GenerateAllPermutations(strategyConfiguration)];
        totalCombinations = allPermutations.Count * totalVariationsPerStrategy;

        Console.WriteLine();

        // Validate that we have a reasonable number of combinations
        if (totalCombinations <= 0)
        {
            throw new InvalidOperationException("No valid parameter combinations found for this strategy type.");
        }
    }

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

    public void Test()
    {
        if (isRunning)
            return;

        isRunning = true;
        totalStopwatch.Restart();

        Console.WriteLine($"- Strategy Name: {strategyName}");
        Console.WriteLine($"- Worker Thread Count: {threadCount}");
        Console.WriteLine($"- Data Points: {historicalData.Count:N0}");
        Console.WriteLine($"- Time Frame: {backtestOptions.TimeFrame}");
        Console.WriteLine($"- Max Concurrent Trades: {string.Join(", ", backtestOptions.MaxConcurrentTrades)}");
        Console.WriteLine($"- Risk Percentage: {string.Join(", ", backtestOptions.RiskPercentage)}");
        Console.WriteLine($"- Max Drawdown: {backtestOptions.MaxDrawdown * 100:F0}%");
        Console.WriteLine($"- Lookback Window: {backtestOptions.LookbackDays} days");
        Console.WriteLine($"- Max Inactivity Percentage: {maxInactivityPercentage * 100:F0}%");
        Console.WriteLine($"- Initial Balance: ${backtestOptions.InitialBalance:F2}");
        Console.WriteLine($"- Fee Percentage: {backtestOptions.FeePercentage:F2}%");
        Console.WriteLine($"- Leverage: {backtestOptions.Leverage}x");

        // Handle loading and testing a saved configuration if the flag is set
        if (runSavedConfiguration)
        {
            Console.WriteLine("\n[Testing Saved Configuration]");
            var savedConfig = LoadFromJson(GenerateResultFilename());

            if (savedConfig == null)
            {
                Console.WriteLine("Error: No saved configuration found. Exiting test.");
                isRunning = false;
                return;
            }

            Console.WriteLine("Loaded configuration:");
            Console.WriteLine(IndicatorCollection.GenerateParameterKey(savedConfig));

            // Create a strategy from the saved configuration
            var strategy = new TradingStrategy(savedConfig);

            // Run a single backtest
            var result = Backtest.Run(strategy, historicalData, backtestOptions);

            // Display result
            Console.WriteLine("\nBacktest Results:");
            Console.WriteLine($"- Net Profit: ${result.NetProfit:F2}");
            Console.WriteLine($"- Return Percentage: {result.ReturnPercentage:F2}%");
            Console.WriteLine($"- Win Rate: {result.WinRatePercent:F2}%");
            Console.WriteLine($"- Total Trades: {result.TotalTrades}");
            Console.WriteLine($"- Winning Trades: {result.WinningTrades}");
            Console.WriteLine($"- Losing Trades: {result.LosingTrades}");
            Console.WriteLine($"- Max Drawdown: {result.MaxDrawdown * 100:F2}%");
            Console.WriteLine($"- Sharpe Ratio: {result.SharpeRatio:F4}");
            Console.WriteLine($"- Trading Activity: {result.TradingActivityPercentage:F2}%");
            if (result.TerminatedEarly)
            {
                Console.WriteLine($"- Terminated Early: {result.TerminationReason}");
            }

            isRunning = false;
            totalStopwatch.Stop();
            return;
        }

        Console.WriteLine($"- Total Combinations: {totalCombinations:N0}");
        Console.WriteLine($"- Variations per Strategy: {totalVariationsPerStrategy}");
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
                        // Use the StrategyFactory with the name and directly pass the indicator collection
                        var strategy = new TradingStrategy(parameter);

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
                            var paramKey = parameterKeys[i % strategies.Count];
                            var paramInstance = parameterInstances[i % strategies.Count];

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
                            else if (result.WinRate > 0.50 && result.NetProfit > 0)
                            {
                                Interlocked.Increment(ref successfulCount);
                                // Create a composite key that includes the parameter config, risk, and max trades
                                string compositeKey = $"{paramKey}_R{result.RiskPercentage}_C{result.MaxConcurrentTrades}";
                                if (!Results.TryAdd(compositeKey, (paramInstance, result)))
                                {
                                    // It's a duplicate - check if the new result is better
                                    if (result.NetProfit > Results[compositeKey].Result.NetProfit)
                                    {
                                        // Replace with better result
                                        Results[compositeKey] = (paramInstance, result);
                                    }
                                    Interlocked.Increment(ref duplicateCount);
                                }
                            }
                            else
                            {
                                Interlocked.Increment(ref failedCount);
                            }
                        }

                        // Update the processed combinations counter to include all parameter variations
                        Interlocked.Add(ref currentCombinationIndex, strategies.Count * totalVariationsPerStrategy);
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
            var key = $"{IndicatorCollection.GenerateParameterKey(result.Params)}_R{result.Result.RiskPercentage}_C{result.Result.MaxConcurrentTrades}";
            newResults.TryAdd(key, result);
        }

        // Replace the old dictionary with the new one
        Results.Clear();
        foreach (var kvp in newResults)
        {
            Results.TryAdd(kvp.Key, kvp.Value);
        }
    }

    private string GenerateResultFilename()
    {
        // Format: StrategyName_LookbackDays_TimeFrame_RiskPercentage_MaxConcurrentTrades.json
        string fileName = $"{strategyName}_{backtestOptions.LookbackDays}days_{backtestOptions.TimeFrame}_{string.Join("_", backtestOptions.RiskPercentage)}R_{string.Join("_", backtestOptions.MaxConcurrentTrades)}C.json";
        return fileName;
    }

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
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
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
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
            };

            string json = JsonSerializer.Serialize(testResults, serializeOptions);
            File.WriteAllText(fileName, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError saving top results: {ex.Message}");
        }
    }

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

            string statusLine = $"\r{new string(progressBar)} {percentage}% ({current:N0}/{totalCombinations:N0}) " + $"Found: {successfulCount:N0}  ";

            // Write to console without a newline
            Console.Write(statusLine);

            // Wait for next update
            Thread.Sleep(500);
        }

        Console.WriteLine();  // End the progress line
    }

    public IndicatorCollection LoadFromJson(string? filename = null)
    {
        // Use the standard filename format if none provided
        filename ??= GenerateResultFilename();

        if (!File.Exists(filename))
        {
            Console.WriteLine($"Results file not found: {filename}");
            return new IndicatorCollection();
        }

        try
        {
            // Load and parse the JSON file
            string json = File.ReadAllText(filename);
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
            };

            var results = JsonSerializer.Deserialize<List<TestResult>>(json, jsonOptions);

            if (results == null || results.Count == 0)
            {
                Console.WriteLine($"No results found in file: {filename}");
                return new IndicatorCollection();
            }

            // Convert TestResults to IndicatorCollections
            var collection = new IndicatorCollection();
            try
            {
                collection = results[0].ToIndicatorCollection();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting result to indicator collection: {ex.Message}");
            }

            Console.WriteLine($"Successfully loaded configuration from {filename}");
            return collection;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading from JSON file: {ex.Message}");
            return new IndicatorCollection();
        }
    }

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
            Console.WriteLine("\nTermination Reasons by Category:");
            Console.WriteLine("==============================");

            // Group termination reasons by category
            var groupedReasons = new Dictionary<string, int>();
            int drawdownCount = 0;
            int inactivityCount = 0;
            int otherCount = 0;

            foreach (var reason in terminationReasons)
            {
                if (reason.Key.StartsWith("Worsening drawdown") || reason.Key.StartsWith("Extreme drawdown"))
                {
                    drawdownCount += reason.Value;
                }
                else if (reason.Key.StartsWith("Extended inactivity"))
                {
                    inactivityCount += reason.Value;
                }
                else
                {
                    // Add other reasons without grouping
                    groupedReasons[reason.Key] = reason.Value;
                    otherCount += reason.Value;
                }
            }

            // Add the grouped reasons
            if (drawdownCount > 0)
            {
                groupedReasons["Drawdown threshold exceeded"] = drawdownCount;
            }

            if (inactivityCount > 0)
            {
                groupedReasons["Extended inactivity periods"] = inactivityCount;
            }

            // Display the grouped reasons
            foreach (var reason in groupedReasons.OrderByDescending(r => r.Value))
            {
                Console.WriteLine($"- {reason.Key}: {reason.Value:N0} strategies ({reason.Value * 100.0 / terminatedEarlyCount:F1}%)");
            }

            // Show the top 3 specific reasons
            Console.WriteLine("\nTop Specific Termination Reasons:");
            Console.WriteLine("================================");
            int count = 0;
            foreach (var reason in terminationReasons.OrderByDescending(r => r.Value))
            {
                Console.WriteLine($"- {reason.Key}: {reason.Value:N0} strategies");
                count++;
                if (count >= 3) break;
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
                             $"Trades: {result.Result.TotalTrades}, " +
                             $"Risk: {result.Result.RiskPercentage}%, " +
                             $"Max Trades: {result.Result.MaxConcurrentTrades}");
        }

        // Save the top results to a file
        if (topResults.Count > 0)
        {
            // Then save all top 10 results
            SaveTopResults();
        }
    }
}

