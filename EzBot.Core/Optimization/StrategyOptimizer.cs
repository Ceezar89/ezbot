using EzBot.Core.Extensions;
using EzBot.Core.Factory;
using EzBot.Core.Indicator;
using EzBot.Core.Strategy;
using EzBot.Models;
using EzBot.Common;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace EzBot.Core.Optimization
{
    public class StrategyOptimizer(
        List<BarData> fullHistoricalData,
        StrategyType strategyType,
        TimeFrame timeFrame = TimeFrame.OneHour,
        double initialBalance = 1000,
        double feePercentage = 0.05,
        int lookbackDays = 1500,
        int threadCount = -1,
        double minTemperature = 5.0,
        double defaultCoolingRate = 0.95,
        int maxConcurrentTrades = 5,
        double maxDrawdownPercent = 30,
        int leverage = 10,
        int daysInactiveLimit = 10,
        double minWinRatePercent = 0.55,
        int minTotalTrades = 10,
        string outputFile = ""
        )
    {
        private readonly List<BarData> fullHistoricalData = fullHistoricalData;
        private readonly StrategyType strategyType = strategyType;
        private readonly TimeFrame timeFrame = timeFrame;
        private readonly double initialBalance = initialBalance;
        private readonly double feePercentage = feePercentage;
        private readonly int maxConcurrentTrades = maxConcurrentTrades;
        private readonly double maxDrawdownPercent = maxDrawdownPercent;
        private readonly int leverage = leverage;
        private readonly int daysInactiveLimit = daysInactiveLimit;
        private readonly double minWinRatePercent = minWinRatePercent;
        private readonly int minTotalTrades = minTotalTrades;
        private readonly double minTemperature = minTemperature;
        private readonly double defaultCoolingRate = defaultCoolingRate;
        private readonly string outputFile = outputFile;
        private readonly ConcurrentBag<(IndicatorCollection Params, BacktestResult Result)> candidates = [];

        private readonly ConcurrentQueue<(IndicatorCollection Params, BacktestResult Result, double Fitness)> results = [];

        // Track both tested hashes and a cache of backtests for similar parameter sets
        private readonly ConcurrentDictionary<int, byte> testedParameterHashes = new();
        private readonly ConcurrentDictionary<int, BacktestResult> backtestCache = new();
        private readonly int maxResultsToKeep = Math.Max(1000, threadCount * 250);

        // Keep track of optimization metrics
        private readonly Stopwatch totalStopwatch = new(); // Total stopwatch for optimization duration
        private long totalBacktestsRun = 0; // Total number of backtests run
        private long cachedBacktestsUsed = 0; // Total number of cached backtests used
        private readonly Lock metricsLock = new(); // Lock for thread-safe metrics updates
        private double averageTemperature = 100.0; // Average temperature for progress reporting
        private double lowestObservedTemperature = 100.0; // Track the lowest temperature we've seen
        private int highTempInjectionCount = 0; // Track how many high temp work items we've injected
        private const int MAX_HIGH_TEMP_INJECTIONS = 10; // Lower the limit
        private DateTime lastSaveTime = DateTime.MinValue; // Track when we last saved results
        private const int SAVE_INTERVAL_SECONDS = 120; // Save every 2 minutes

        // For tracking and termination conditions
        private const int MAX_ITERATIONS_WITHOUT_IMPROVEMENT = 2000;  // Terminate if no improvement for this many iterations
        private readonly Lock globalImprovement_lock = new();
        private double globalBestFitness = double.MinValue;
        private int iterationsSinceGlobalImprovement = 0;
        private double explorationRate = 1.0; // Controls how often we inject random exploration
        private const double MIN_EXPLORATION_RATE = 0.05; // Minimum exploration to maintain
        private double globalCoolingMultiplier = 1.0; // Can accelerate cooling when progress stalls
        private readonly Queue<double> recentFitnessValues = new(10); // For convergence detection
        private int convergenceCounter = 0;
        private const int CONVERGENCE_CHECK_INTERVAL = 500;

        private readonly Lock highTempLock = new();
        private const double ABSOLUTE_MAX_TEMPERATURE = 100.0;

        private DateTime lastCacheSaveTime = DateTime.MinValue;
        private const int CACHE_SAVE_INTERVAL_SECONDS = 300; // Save cache every 5 minutes

        /// <summary>
        /// Find optimal parameters for the strategy using simulated annealing with work stealing.
        /// </summary>
        public OptimizationResult FindOptimalParameters()
        {
            totalStopwatch.Start();

            // Load any existing backtest cache
            LoadCacheIfExists();

            List<BarData> historicalData = [.. fullHistoricalData.Skip(fullHistoricalData.Count - lookbackDays * 24 * 60)];
            Console.WriteLine($"Loaded {historicalData.Count:N0} bars of historical data.");

            // Convert the data to the desired timeframe
            var timeframeData = TimeFrameUtility.ConvertTimeFrame(historicalData, timeFrame);

            // Determine the number of worker threads
            int workerCount = threadCount;
            if (workerCount < 1) workerCount = 1;

            // Set minimum runtime to 2 minutes
            const int MinimumRuntimeMinutes = 2;
            TimeSpan minimumRuntime = TimeSpan.FromMinutes(MinimumRuntimeMinutes);

            // Step 1: Find good initial candidates in parallel
            var candidateTasks = new List<Task>();

            Console.WriteLine($"Starting {workerCount} randomization search tasks to find seed candidates.");

            // Launch worker tasks to find good initial candidates in parallel
            for (int i = 0; i < workerCount; i++)
            {
                candidateTasks.Add(Task.Run(() =>
                {
                    FindSeedCandidate(timeframeData);
                }));
            }

            // Wait for at least one thread to find a good candidate, or until all tasks complete
            while (candidates.IsEmpty && !Task.WhenAll([.. candidateTasks]).IsCompleted)
            {
                Thread.Sleep(100); // Short sleep to avoid tight loop
            }

            Console.WriteLine($"Waiting for {candidateTasks.Count} randomization search tasks to complete...");

            // Wait for all tasks to complete
            Task.WaitAll([.. candidateTasks]);

            // Step 2: Use the candidates as seeds for simulated annealing with work stealing

            // Find the best candidate to use as seed
            var (bestParams, bestResult) = candidates
                .OrderByDescending(c => CalculateFitness(c.Result))
                .First();

            // Create a shared work queue for all threads
            var sharedWorkQueue = new ConcurrentQueue<WorkItem>();

            // Maintain a best global result for exploration
            var globalBestResult = new AtomicReference<(IndicatorCollection Params, BacktestResult Result, double Fitness)>(
                (bestParams.DeepClone(), bestResult, CalculateFitness(bestResult)));

            // Initialize the queue with multiple starting points derived from the best candidate
            var parameterPerturbator = new ParameterPerturbator();

            // Add the original unperturbed best parameters first to preserve high win-rate configuration
            sharedWorkQueue.Enqueue(new WorkItem
            {
                Parameters = bestParams.DeepClone(), // Unperturbed original
                Temperature = 100.0, // Initial temperature
                PreviousBestWinRate = 0,
                IterationsSinceImprovement = 0,
                TotalIterations = 0,
                AdaptiveCoolingRate = defaultCoolingRate, // Use the global cooling rate
            });

            // Also add the original to the results collection so it won't be lost
            results.Enqueue((bestParams.DeepClone(), bestResult, CalculateFitness(bestResult)));

            // Now add perturbed variations to explore the parameter space
            for (int i = 0; i < workerCount * 10; i++)
            {
                var randomSeed = new Random(Guid.NewGuid().GetHashCode());
                var params_copy = bestParams.DeepClone();
                ParameterPerturbator.PerturbParameters(params_copy, 0.2, randomSeed);

                sharedWorkQueue.Enqueue(new WorkItem
                {
                    Parameters = params_copy,
                    Temperature = 100.0, // Initial temperature
                    PreviousBestWinRate = 0,
                    IterationsSinceImprovement = 0,
                    TotalIterations = 0,
                    AdaptiveCoolingRate = defaultCoolingRate, // Use the global cooling rate
                });
            }

            // Create a completion signal that will be set when the queue is empty
            // and all workers are idle
            var completionSignal = new ManualResetEventSlim(false);
            int activeWorkers = workerCount;

            // Add a periodic progress reporter
            var progressReporter = new Timer(_ => ReportProgress(globalBestResult), null, 5000, 5000);

            Console.WriteLine($"\nStarting dynamic work distribution with {workerCount} worker threads:\n");

            // Start worker tasks
            var workerTasks = new List<Task>();
            for (int i = 0; i < workerCount; i++)
            {
                int workerId = i;
                workerTasks.Add(Task.Run(() =>
                {
                    ProcessWorkQueue(
                        timeframeData,
                        sharedWorkQueue,
                        workerId,
                        completionSignal,
                        ref activeWorkers,
                        globalBestResult,
                        parameterPerturbator);
                }));
            }

            // Wait for all worker tasks to complete, but ensure minimum runtime
            Task.Run(() =>
            {
                // Wait for the completion signal
                completionSignal.Wait();

                // Check if we've reached minimum runtime
                if (totalStopwatch.Elapsed < minimumRuntime)
                {
                    Console.WriteLine($"Minimum runtime of {MinimumRuntimeMinutes} minutes not reached yet. Continuing optimization...");

                    // Reset active workers count
                    Interlocked.Exchange(ref activeWorkers, workerCount);

                    // Inject new work items
                    for (int i = 0; i < workerCount * 5; i++) // Reduce from 10 to 5
                    {
                        var randomSeed = new Random(Guid.NewGuid().GetHashCode());
                        if (i % 2 == 0 && TryIncrementHighTempInjection()) // Use our high temp control method
                        {
                            // Add random exploration
                            var freshParams = new IndicatorCollection(strategyType);
                            freshParams.RandomizeParameters();
                            sharedWorkQueue.Enqueue(new WorkItem
                            {
                                Parameters = freshParams,
                                Temperature = GetSafeTemperature(60.0), // Limit to 60 instead of 100
                                PreviousBestWinRate = 0,
                                IterationsSinceImprovement = 0,
                                TotalIterations = 0,
                                AdaptiveCoolingRate = 0.95,
                            });
                        }
                        else
                        {
                            // Add perturbed version of the best
                            var bestParams = globalBestResult.Value.Params;
                            if (bestParams != null)
                            {
                                var newParams = bestParams.DeepClone();
                                ParameterPerturbator.PerturbParameters(newParams, 0.4, randomSeed);
                                sharedWorkQueue.Enqueue(new WorkItem
                                {
                                    Parameters = newParams,
                                    Temperature = GetSafeTemperature(40.0), // Limit to 40 instead of 60
                                    PreviousBestWinRate = 0,
                                    IterationsSinceImprovement = 0,
                                    TotalIterations = 0,
                                    AdaptiveCoolingRate = defaultCoolingRate,
                                });
                            }
                        }
                    }

                    // Reset completion signal
                    completionSignal.Reset();

                    // Restart worker tasks if they've completed
                    for (int i = 0; i < workerCount; i++)
                    {
                        int workerId = i;
                        if (workerTasks[i].IsCompleted)
                        {
                            workerTasks[i] = Task.Run(() =>
                            {
                                ProcessWorkQueue(
                                    timeframeData,
                                    sharedWorkQueue,
                                    workerId,
                                    completionSignal,
                                    ref activeWorkers,
                                    globalBestResult,
                                    parameterPerturbator);
                            });
                        }
                    }

                    // Wait for completion signal again
                    completionSignal.Wait();
                }
            }).Wait();

            progressReporter.Dispose();

            totalStopwatch.Stop();
            Console.WriteLine("All workers completed. Processing results...");
            Console.WriteLine($"Total optimization time: {totalStopwatch.Elapsed.TotalMinutes:F1} minutes");
            Console.WriteLine($"Total backtests run: {totalBacktestsRun:N0}, cached backtests used: {cachedBacktestsUsed:N0}");
            Console.WriteLine($"Cache hit rate: {cachedBacktestsUsed * 100.0 / (totalBacktestsRun + cachedBacktestsUsed):F1}%");

            // Get the best result from the queue by sorting
            var allResults = results.OrderByDescending(r => r.Fitness).ToList();

            // Filter for valid results
            var validResults = allResults
                .Where(r => r.Result.TotalTrades > minTotalTrades && r.Result.MaxDrawdownPercent <= maxDrawdownPercent)
                .ToList();

            // Check if we have valid results
            if (validResults.Count == 0)
            {
                if (allResults.Count > 0)
                {
                    // If we have some results but none are valid, return the best of what we have
                    var (Params, Result, Fitness) = allResults.First();
                    return new OptimizationResult
                    {
                        BestParameters = Params.ToDto(),
                        BacktestResult = Result,
                    };
                }

                // No results at all
                return new OptimizationResult
                {
                    BestParameters = new IndicatorCollection(strategyType).ToDto(),
                    BacktestResult = new BacktestResult(),
                };
            }

            // Use the best valid result
            var (bestParameters, finalResult, _) = validResults.First();

            // Log the total number of unique parameter combinations tested
            Console.WriteLine($"Total unique parameter combinations tested: {testedParameterHashes.Count:N0}");

            // Add cache saving at the end of the method, just before returning the result
            // Add right before the final return statement
            SaveCacheIfNeeded(); // Save the final cache before exiting

            return new OptimizationResult
            {
                BestParameters = bestParameters.ToDto(),
                BacktestResult = finalResult,
            };
        }

        private void ReportProgress(AtomicReference<(IndicatorCollection Params, BacktestResult Result, double Fitness)> globalBest)
        {
            var (_, Result, Fitness) = globalBest.Value;
            var elapsed = totalStopwatch.Elapsed;

            Console.WriteLine();
            Console.WriteLine($"==================================================================");
            Console.WriteLine($"Progress Report at {elapsed.TotalMinutes:F1} minutes");
            Console.WriteLine($"Best fitness: {Fitness:F2}, Win rate: {Result.WinRatePercent:F2}%, Net profit: {Result.NetProfit:F2}");
            Console.WriteLine($"Max drawdown: {Result.MaxDrawdownPercent:F2}%, Total trades: {Result.TotalTrades}");
            Console.WriteLine($"Avg profit per trade: {(Result.TotalTrades > 0 ? Result.NetProfit / Result.TotalTrades : 0):F2}");
            Console.WriteLine($"Parameters tested: {testedParameterHashes.Count:N0}, Results collected: {results.Count:N0}");
            Console.WriteLine($"Backtests run: {totalBacktestsRun:N0}, cached hits: {cachedBacktestsUsed:N0} ({(cachedBacktestsUsed * 100.0 / (totalBacktestsRun + cachedBacktestsUsed)):F1}%)");

            // Modified temperature section with better information
            double tempProgress = Math.Min(100.0, Math.Max(0.0, 100.0 * (1.0 - ((averageTemperature - minTemperature) / (100.0 - minTemperature)))));
            Console.WriteLine($"Average temperature: {averageTemperature:F2} (min observed: {lowestObservedTemperature:F2}, cooling: {tempProgress:F1}%)");
            Console.WriteLine($"Iterations since improvement: {iterationsSinceGlobalImprovement}, Exploration rate: {explorationRate:F2}");
            Console.WriteLine($"Global cooling multiplier: {globalCoolingMultiplier:F2}, High temp injections: {highTempInjectionCount}");

            Console.WriteLine("==================================================================");
            Console.WriteLine();

            // Periodically save the current best result
            SaveProgressIfNeeded(globalBest.Value);

            // Also save the backtest cache periodically
            SaveCacheIfNeeded();
        }

        private void SaveProgressIfNeeded((IndicatorCollection Params, BacktestResult Result, double Fitness) currentBest)
        {
            // Skip if no output file specified or not enough time has passed since last save
            if (string.IsNullOrEmpty(outputFile) ||
                (DateTime.Now - lastSaveTime).TotalSeconds < SAVE_INTERVAL_SECONDS)
                return;

            // Only save if we have valid results
            if (currentBest.Result.TotalTrades > 0)
            {
                bool shouldSave = true;

                // Check if file exists and compare results
                if (File.Exists(outputFile))
                {
                    try
                    {
                        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                        string previousJson = File.ReadAllText(outputFile);
                        var previousResult = JsonSerializer.Deserialize<OptimizationResult>(previousJson, jsonOptions);

                        // save if better result else do nothing
                        if (previousResult != null && currentBest.Result.NetProfit > previousResult.BacktestResult.NetProfit)
                        {
                            Console.WriteLine($"Current result has better net profit than previous result. Saving...");
                        }
                        else
                        {
                            shouldSave = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading previous result: {ex.Message}");
                        Console.WriteLine("Will save current result.");
                    }
                }

                if (shouldSave)
                {
                    try
                    {
                        var optimizationResult = new OptimizationResult
                        {
                            BestParameters = currentBest.Params.ToDto(),
                            BacktestResult = currentBest.Result,
                        };

                        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                        string json = JsonSerializer.Serialize(optimizationResult, jsonOptions);
                        File.WriteAllText(outputFile, json);
                        Console.WriteLine($"Progress saved to {outputFile}");
                        lastSaveTime = DateTime.Now;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error saving progress: {ex.Message}");
                    }
                }
            }
        }

        private void FindSeedCandidate(
            List<BarData> timeframeData
            )
        {
            var parameterPerturbator = new ParameterPerturbator();
            int searchAttempts = 0;
            const int MaxSearchAttempts = 100;

            // Keep searching until we find a good candidate
            while (candidates.IsEmpty)
            {
                searchAttempts++;

                // Create a random set of parameters
                var candidateParams = new IndicatorCollection(strategyType);
                candidateParams.RandomizeParameters();

                // Discretize and check if we've already tested this parameter set
                int discretizedHash = parameterPerturbator.GetDiscretizedHash(candidateParams);
                if (testedParameterHashes.ContainsKey(discretizedHash))
                {
                    continue; // Skip testing this parameter set
                }

                // Mark this parameter set as tested
                testedParameterHashes.TryAdd(discretizedHash, 0);

                // Evaluate the candidate
                var candidateStrategy = StrategyFactory.CreateStrategy(strategyType, candidateParams);
                var candidateResult = RunBacktest(candidateStrategy, timeframeData, discretizedHash);

                // Check if this candidate meets our criteria - with a more lenient drawdown limit
                if (candidateResult.TotalTrades > minTotalTrades &&
                    candidateResult.MaxDrawdownPercent <= maxDrawdownPercent &&
                    candidateResult.WinRate >= minWinRatePercent)
                {
                    Console.WriteLine($"Found a good seed candidate with fitness: {CalculateFitness(candidateResult):F2}, win rate: {candidateResult.WinRatePercent:F2}%, drawdown: {candidateResult.MaxDrawdownPercent:F2}%");
                    // Take a snapshot of the current candidates
                    var snapshot = candidates.ToArray();

                    // If the bag is empty or our candidate is better than existing ones, add it
                    if (snapshot.Length == 0)
                    {
                        candidates.Add((candidateParams, candidateResult));
                        return;
                    }
                    else
                    {
                        // Find the best candidate in the current snapshot
                        double bestFitness = snapshot.Max(c => CalculateFitness(c.Result));
                        double currentFitness = CalculateFitness(candidateResult);

                        // Add our candidate if it's better
                        if (currentFitness > bestFitness)
                        {
                            candidates.Add((candidateParams, candidateResult));
                        }
                        return;
                    }
                }

                // Fallback: After many attempts, accept any candidate that has enough trades
                // even if it doesn't meet the win rate criteria
                if (searchAttempts > MaxSearchAttempts && candidateResult.TotalTrades >= minTotalTrades &&
                    candidateResult.MaxDrawdownPercent <= maxDrawdownPercent * 2)
                {
                    Console.WriteLine($"Using fallback candidate with {candidateResult.TotalTrades} trades, win rate: {candidateResult.WinRatePercent:F2}%");
                    candidates.Add((candidateParams, candidateResult));
                    return;
                }
            }
        }

        // Process items from the work queue until it's empty
        private void ProcessWorkQueue(
            List<BarData> timeframeData,
            ConcurrentQueue<WorkItem> workQueue,
            int workerId,
            ManualResetEventSlim completionSignal,
            ref int activeWorkers,
            AtomicReference<(IndicatorCollection Params, BacktestResult Result, double Fitness)> globalBest,
            ParameterPerturbator parameterPerturbator)
        {
            const double MinWinRateImprovement = 0.01;
            const int MaxConsecutiveIterations = 10;

            try
            {
                Random random = new(Guid.NewGuid().GetHashCode() + workerId);
                int itemsProcessed = 0;
                long workerBacktestsRun = 0;
                long workerCachedHits = 0;

                // Keep a thread-local best result for faster access
                (IndicatorCollection Params, BacktestResult Result, double Fitness)? threadLocalBest = null;

                // Adaptive annealing parameters
                double successRate = 0.5;  // Initial success rate (ratio of accepted to total moves)
                int acceptedMoves = 0;
                int totalMoves = 0;
                const int adaptationInterval = 50; // How often to adapt parameters based on success rate

                // Periodically check termination conditions
                int terminationCheckCounter = 0;

                while (workQueue.TryDequeue(out var workItem))
                {
                    itemsProcessed++;
                    terminationCheckCounter++;

                    // Check for early termination periodically
                    if (terminationCheckCounter % 100 == 0 && workerId == 0)
                    {
                        CheckAndTerminate(workQueue, completionSignal, ref activeWorkers);
                    }

                    // Process this work item (one iteration of simulated annealing)
                    var currentParams = workItem.Parameters;
                    double temperature = workItem.Temperature;
                    double coolingRate = workItem.AdaptiveCoolingRate;
                    double previousBestWinRate = workItem.PreviousBestWinRate;
                    int iterationsSinceImprovement = workItem.IterationsSinceImprovement;
                    int totalIterations = workItem.TotalIterations;

                    // Update average temperature (infrequently to reduce lock contention)
                    if (random.NextDouble() < 0.05)
                    {
                        lock (metricsLock)
                        {
                            // Use our improved temperature tracking
                            UpdateAverageTemperature(temperature);

                            // Update global status/exploration metrics
                            UpdateExplorationRate();
                            UpdateGlobalCooling();
                        }
                    }

                    // Create a strategy and evaluate it
                    var currentStrategy = StrategyFactory.CreateStrategy(strategyType, currentParams);

                    // Use a discretized hash for caching
                    int currentHash = parameterPerturbator.GetDiscretizedHash(currentParams);
                    var currentResult = RunBacktest(currentStrategy, timeframeData, currentHash);

                    workerBacktestsRun++;

                    // Track if this was a new backtest or used the cache
                    if (backtestCache.ContainsKey(currentHash))
                    {
                        workerCachedHits++;
                    }

                    double currentFitness = CalculateFitness(currentResult);
                    double currentWinRate = currentResult.WinRate;

                    // Update thread local best
                    if (threadLocalBest == null || currentFitness > threadLocalBest.Value.Fitness)
                    {
                        threadLocalBest = (currentParams.DeepClone(), currentResult, currentFitness);
                    }

                    // Store valid results, keeping only the best up to maxResultsToKeep
                    if (currentResult.TotalTrades > minTotalTrades && currentResult.MaxDrawdownPercent <= maxDrawdownPercent)
                    {
                        var resultTuple = (SafeClone(currentParams), currentResult, currentFitness);

                        // Add to results and trim occasionally
                        results.Enqueue(resultTuple);

                        // Occasionally trim the results collection to prevent unbounded growth
                        if (results.Count > maxResultsToKeep && random.NextDouble() < 0.1)
                        {
                            TrimResultsCollection();
                        }

                        // Check if this is the best result so far and update global improvement tracking
                        var currentBest = globalBest.Value;

                        lock (globalImprovement_lock)
                        {
                            if (currentFitness > globalBestFitness)
                            {
                                globalBestFitness = currentFitness;
                                iterationsSinceGlobalImprovement = 0;

                                // Save the best result immediately when found
                                if (currentFitness > currentBest.Fitness)
                                {
                                    globalBest.Value = resultTuple;
                                    SaveProgressIfNeeded(globalBest.Value);
                                }
                            }
                            else
                            {
                                iterationsSinceGlobalImprovement++;
                            }
                        }
                    }

                    // Adaptive cooling - check if we should adjust based on success rate
                    if (totalMoves > 0 && totalMoves % adaptationInterval == 0)
                    {
                        successRate = (double)acceptedMoves / totalMoves;
                        // Adjust cooling rate based on success rate
                        // If success rate is high, cool slower to explore more
                        // If success rate is low, cool faster to exploit current good solutions
                        coolingRate = Math.Max(0.5, Math.Min(0.95, defaultCoolingRate + (successRate - 0.5) * 0.1));
                    }

                    // If we've reached the temperature lower bound or exhausted iterations,
                    // don't generate a new work item
                    bool shouldStop = temperature <= minTemperature ||
                                     (previousBestWinRate > 0 &&
                                      currentWinRate - previousBestWinRate < MinWinRateImprovement &&
                                      iterationsSinceImprovement >= MaxConsecutiveIterations);

                    // Check for convergence - if thread local and global best are very close
                    if (threadLocalBest != null && globalBest.Value.Fitness > 0 &&
                        Math.Abs(threadLocalBest.Value.Fitness - globalBest.Value.Fitness) / globalBest.Value.Fitness < 0.01 &&
                        random.NextDouble() < 0.2 * explorationRate) // Scale with exploration rate
                    {
                        // Only inject high temperature items if we haven't injected too many already
                        if (TryIncrementHighTempInjection() && iterationsSinceGlobalImprovement < 500)
                        {
                            // With some probability, create a fresh random starting point
                            var freshParams = new IndicatorCollection(strategyType);
                            freshParams.RandomizeParameters();

                            // Add a completely new work item with safe temperature
                            double safeTemp = GetSafeTemperature(100.0);
                            workQueue.Enqueue(new WorkItem
                            {
                                Parameters = freshParams,
                                Temperature = safeTemp,
                                PreviousBestWinRate = 0,
                                IterationsSinceImprovement = 0,
                                TotalIterations = 0,
                                AdaptiveCoolingRate = defaultCoolingRate,
                            });
                        }
                    }

                    if (shouldStop)
                    {
                        // Occasionally reinject a perturbed version of the global best
                        if (random.NextDouble() < 0.2 * explorationRate) // Scale with exploration rate
                        {
                            // Get global best params and create new exploration point
                            var bestParams = globalBest.Value.Params;
                            if (bestParams != null)
                            {
                                var newParams = bestParams.DeepClone();
                                ParameterPerturbator.PerturbParameters(newParams, 0.3, random);
                                workQueue.Enqueue(new WorkItem
                                {
                                    Parameters = newParams,
                                    Temperature = 50.0, // Start at medium temperature
                                    PreviousBestWinRate = 0,
                                    IterationsSinceImprovement = 0,
                                    TotalIterations = 0,
                                    AdaptiveCoolingRate = defaultCoolingRate,
                                });
                            }
                        }
                        continue;
                    }

                    // Increment total moves for adaptive cooling
                    totalMoves++;

                    // Create a new candidate solution
                    double temperatureRatio = temperature / 100.0; // Normalized ratio
                    var candidateParams = currentParams.DeepClone();
                    ParameterPerturbator.PerturbParameters(candidateParams, temperatureRatio, random);

                    // Check if we've already tested this parameter set
                    int discretizedHash = parameterPerturbator.GetDiscretizedHash(candidateParams);
                    if (testedParameterHashes.ContainsKey(discretizedHash))
                    {
                        // Cool down and requeue if this is a duplicate
                        workItem.Temperature = Math.Min(workItem.Temperature * coolingRate * globalCoolingMultiplier, ABSOLUTE_MAX_TEMPERATURE);
                        workItem.TotalIterations++;
                        workItem.IterationsSinceImprovement++;
                        workItem.AdaptiveCoolingRate = coolingRate;
                        workQueue.Enqueue(workItem);
                        continue;
                    }

                    // Mark this parameter set as tested
                    testedParameterHashes.TryAdd(discretizedHash, 0);

                    // Create and evaluate the candidate solution
                    var candidateStrategy = StrategyFactory.CreateStrategy(strategyType, candidateParams);
                    var candidateResult = RunBacktest(candidateStrategy, timeframeData, discretizedHash);
                    workerBacktestsRun++;

                    // Track if this used the cache
                    if (backtestCache.ContainsKey(discretizedHash))
                    {
                        workerCachedHits++;
                    }

                    // Skip invalid results
                    if (candidateResult.TotalTrades == 0 || candidateResult.MaxDrawdownPercent > maxDrawdownPercent)
                    {
                        // Still add this iteration to the work queue with cooled temperature
                        workItem.Temperature = Math.Min(workItem.Temperature * coolingRate * globalCoolingMultiplier, ABSOLUTE_MAX_TEMPERATURE);
                        workItem.TotalIterations++;
                        workItem.IterationsSinceImprovement++;
                        workItem.AdaptiveCoolingRate = coolingRate;
                        workQueue.Enqueue(workItem);
                        continue;
                    }

                    double candidateFitness = CalculateFitness(candidateResult);
                    double candidateWinRate = candidateResult.WinRate;

                    // Calculate acceptance probability
                    double energyDelta = -candidateFitness - (-currentFitness);
                    double acceptanceProbability =
                        (energyDelta <= 0) ? 1.0 : Math.Exp(-energyDelta / temperature);

                    // Accept the candidate solution with some probability
                    if (random.NextDouble() < acceptanceProbability)
                    {
                        acceptedMoves++; // Track for adaptive cooling

                        // Store it in results
                        var resultTuple = (SafeClone(candidateParams), candidateResult, candidateFitness);
                        results.Enqueue(resultTuple);

                        // Occasionally trim results
                        if (results.Count > maxResultsToKeep && random.NextDouble() < 0.1)
                        {
                            TrimResultsCollection();
                        }

                        // Update global best tracking
                        lock (globalImprovement_lock)
                        {
                            if (candidateFitness > globalBestFitness)
                            {
                                globalBestFitness = candidateFitness;
                                iterationsSinceGlobalImprovement = 0;
                            }
                            else
                            {
                                iterationsSinceGlobalImprovement++;
                            }
                        }

                        // Update thread local best if needed
                        if (threadLocalBest == null || candidateFitness > threadLocalBest.Value.Fitness)
                        {
                            threadLocalBest = resultTuple;

                            // Check if this is the new global best
                            var currentBest = globalBest.Value;
                            if (candidateFitness > currentBest.Fitness)
                            {
                                globalBest.Value = resultTuple;
                            }
                        }

                        // Check if win rate improved
                        bool winRateImproved = candidateWinRate > currentWinRate;
                        double newBestWinRate = winRateImproved ? candidateWinRate : currentWinRate;

                        // Create a new work item with the accepted solution
                        workQueue.Enqueue(new WorkItem
                        {
                            Parameters = candidateParams,
                            Temperature = Math.Min(temperature * coolingRate * globalCoolingMultiplier, ABSOLUTE_MAX_TEMPERATURE), // Apply temperature cap
                            PreviousBestWinRate = previousBestWinRate > 0 ? previousBestWinRate : currentWinRate,
                            IterationsSinceImprovement = winRateImproved ? 0 : iterationsSinceImprovement + 1,
                            TotalIterations = totalIterations + 1,
                            AdaptiveCoolingRate = coolingRate,
                        });
                    }
                    else
                    {
                        // Reject but create a new work item with the original solution and cooled temperature
                        workQueue.Enqueue(new WorkItem
                        {
                            Parameters = currentParams,
                            Temperature = Math.Min(temperature * coolingRate * globalCoolingMultiplier, ABSOLUTE_MAX_TEMPERATURE), // Apply temperature cap
                            PreviousBestWinRate = previousBestWinRate > 0 ? previousBestWinRate : currentWinRate,
                            IterationsSinceImprovement = iterationsSinceImprovement + 1,
                            TotalIterations = totalIterations + 1,
                            AdaptiveCoolingRate = coolingRate,
                        });
                    }

                    // Occasionally add diversity with different strategies, scaled by exploration rate
                    if (random.NextDouble() < 0.05 * explorationRate)
                    {
                        // Strategy 1: Use best from thread local cache
                        if (threadLocalBest.HasValue)
                        {
                            var diverseParams = threadLocalBest.Value.Params.DeepClone();
                            ParameterPerturbator.PerturbParameters(diverseParams, 0.3, random);
                            workQueue.Enqueue(new WorkItem
                            {
                                Parameters = diverseParams,
                                Temperature = 30.0, // Medium-low temperature 
                                PreviousBestWinRate = 0,
                                IterationsSinceImprovement = 0,
                                TotalIterations = 0,
                                AdaptiveCoolingRate = defaultCoolingRate,
                            });
                        }
                    }
                    else if (random.NextDouble() < 0.07 * explorationRate)
                    {
                        // Strategy 2: Use global best (shared across all threads)
                        var bestGlobal = globalBest.Value;
                        if (bestGlobal.Params != null)
                        {
                            var diverseParams = bestGlobal.Params.DeepClone();
                            ParameterPerturbator.PerturbParameters(diverseParams, 0.3, random);
                            workQueue.Enqueue(new WorkItem
                            {
                                Parameters = diverseParams,
                                Temperature = 20.0, // Lower temperature for exploitation
                                PreviousBestWinRate = 0,
                                IterationsSinceImprovement = 0,
                                TotalIterations = 0,
                                AdaptiveCoolingRate = defaultCoolingRate,
                            });
                        }
                    }
                    else if (random.NextDouble() < 0.03 * explorationRate)
                    {
                        // Only inject if we haven't hit our limit and we're still in early exploration
                        if (TryIncrementHighTempInjection() && iterationsSinceGlobalImprovement < 500)
                        {
                            // Strategy 3: Fresh random parameters for exploration
                            var freshParams = new IndicatorCollection(strategyType);
                            freshParams.RandomizeParameters();

                            // Use safe temperature
                            double safeTemp = GetSafeTemperature(70.0); // Lower from 100 to 70
                            workQueue.Enqueue(new WorkItem
                            {
                                Parameters = freshParams,
                                Temperature = safeTemp,
                                PreviousBestWinRate = 0,
                                IterationsSinceImprovement = 0,
                                TotalIterations = 0,
                                AdaptiveCoolingRate = defaultCoolingRate,
                            });
                        }
                    }
                }

                // Update global metrics before exit
                lock (metricsLock)
                {
                    totalBacktestsRun += workerBacktestsRun;
                    cachedBacktestsUsed += workerCachedHits;
                }

                // If the queue is empty but there are still active workers, inject new work
                // Scale the number of injections based on exploration rate
                if (workQueue.IsEmpty && activeWorkers > 1)
                {
                    // Calculate how many items to inject based on exploration rate
                    int itemsToInject = (int)Math.Ceiling(3 * explorationRate); // Reduce from 5 to 3

                    if (itemsToInject > 0 && iterationsSinceGlobalImprovement < 800) // Add stricter time limit
                    {
                        int actualInjected = 0;

                        // Add diversity by creating fresh random parameters
                        for (int i = 0; i < itemsToInject; i++)
                        {
                            // Only inject if we're still under our limit
                            if (TryIncrementHighTempInjection())
                            {
                                actualInjected++;

                                var freshParams = new IndicatorCollection(strategyType);
                                freshParams.RandomizeParameters();

                                // Use significantly lower temperature for late-stage injections
                                double safeTemp = GetSafeTemperature(30.0); // Much lower temperature
                                workQueue.Enqueue(new WorkItem
                                {
                                    Parameters = freshParams,
                                    Temperature = safeTemp,
                                    PreviousBestWinRate = 0,
                                    IterationsSinceImprovement = 0,
                                    TotalIterations = 0,
                                    AdaptiveCoolingRate = defaultCoolingRate,
                                });

                                // Also add a perturbed version of the global best if available
                                var bestGlobal = globalBest.Value;
                                if (bestGlobal.Params != null)
                                {
                                    var diverseParams = bestGlobal.Params.DeepClone();
                                    ParameterPerturbator.PerturbParameters(diverseParams, 0.3, random);
                                    workQueue.Enqueue(new WorkItem
                                    {
                                        Parameters = diverseParams,
                                        // Very low temperature for exploitation
                                        Temperature = GetSafeTemperature(20.0),
                                        PreviousBestWinRate = 0,
                                        IterationsSinceImprovement = 0,
                                        TotalIterations = 0,
                                        AdaptiveCoolingRate = defaultCoolingRate,
                                    });
                                }
                            }
                        }

                        if (actualInjected > 0)
                        {
                            Console.WriteLine($"Worker {workerId}: Queue was empty, injected {actualInjected * 2} new items (exploration rate: {explorationRate:F2})");
                        }
                        else
                        {
                            Console.WriteLine($"Worker {workerId}: Queue was empty, no items injected (injection limit reached)");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Worker {workerId}: Queue was empty, no items injected (exploration phase ending)");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Worker {workerId} exception: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                // Signal that this worker is done
                if (Interlocked.Decrement(ref activeWorkers) == 0)
                {
                    Console.WriteLine("All workers have completed. Signaling main thread.");
                    completionSignal.Set();
                }
            }
        }

        // Trim the results collection to maintain a bounded size
        private void TrimResultsCollection()
        {
            try
            {
                // Fast approximation: dequeue some items
                if (results.Count <= maxResultsToKeep) return;

                // Get all items, sort, and keep best N
                var allResults = results.ToArray();

                // Clear existing results
                while (results.TryDequeue(out _)) { }

                // Use fitness diversity to keep a more varied set
                var diverseResults = new List<(IndicatorCollection Params, BacktestResult Result, double Fitness)>();

                // Keep the top 20% best results
                int topResultsCount = maxResultsToKeep / 5;
                diverseResults.AddRange(allResults.OrderByDescending(r => r.Fitness).Take(topResultsCount));

                // For the rest, select diverse results based on a combination of fitness and trade count variety
                var remainingResults = allResults.Except(diverseResults).ToList();

                // First group by trade count ranges
                var tradeCountGroups = remainingResults
                    .GroupBy(r => Math.Floor(r.Result.TotalTrades / 10.0) * 10) // Group by 10s of trades (0-9, 10-19, etc.)
                    .OrderByDescending(g => g.Key);

                // Take best few from each group to ensure diversity in trade counts
                foreach (var group in tradeCountGroups)
                {
                    // Take top items from this trade count group, up to about 10% of max results per group
                    diverseResults.AddRange(group.OrderByDescending(r => r.Fitness).Take(maxResultsToKeep / 10));

                    // Stop if we've collected enough
                    if (diverseResults.Count >= maxResultsToKeep / 2)
                        break;
                }

                // If we still have space, fill with overall best remaining
                int remaining = maxResultsToKeep / 2 - diverseResults.Count;
                if (remaining > 0)
                {
                    diverseResults.AddRange(
                        remainingResults
                        .Except(diverseResults)
                        .OrderByDescending(r => r.Fitness)
                        .Take(remaining));
                }

                // Re-add diverse results to the queue
                foreach (var result in diverseResults)
                {
                    results.Enqueue(result);
                }

                Console.WriteLine($"Trimmed results from {allResults.Length} to {results.Count}, preserving diversity");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during results trimming: {ex.Message}");
                // Fallback to a simpler approach if the sophisticated trimming fails
                try
                {
                    // Use a simple, more robust approach
                    var allResults = results.ToArray();

                    // Clear the queue
                    while (results.TryDequeue(out _)) { }

                    // Take top results based on fitness only
                    var simpleResults = allResults
                        .OrderByDescending(r => r.Fitness)
                        .Take(maxResultsToKeep)
                        .ToList();

                    // Re-add to queue
                    foreach (var result in simpleResults)
                        results.Enqueue(result);

                    Console.WriteLine($"Fallback trimming completed: kept top {simpleResults.Count} results by fitness");
                }
                catch (Exception innerEx)
                {
                    Console.WriteLine($"Critical error in results management: {innerEx.Message}");
                    // At this point we can't recover the results collection,
                    // but the algorithm can continue with an empty results set
                }
            }
        }

        // Work item for the processing queue with improved fields
        private class WorkItem
        {
            public required IndicatorCollection Parameters { get; set; }
            public double Temperature { get; set; }
            public double PreviousBestWinRate { get; set; }
            public int IterationsSinceImprovement { get; set; }
            public int TotalIterations { get; set; }
            public double AdaptiveCoolingRate { get; set; } // Adaptive cooling rate
        }

        // Gets a hash code based on discretized parameter values
        private BacktestResult RunBacktest(ITradingStrategy strategy, List<BarData> historicalData, int paramHash)
        {
            // Check if we've cached this result
            if (backtestCache.TryGetValue(paramHash, out var cachedResult))
            {
                Interlocked.Increment(ref cachedBacktestsUsed);
                return cachedResult;
            }

            Interlocked.Increment(ref totalBacktestsRun);

            var account = new BacktestAccount(initialBalance, feePercentage, leverage);
            Dictionary<int, TradeOrder> activeOrders = [];

            // Skip some initial bars to allow indicators to initialize
            int warmupPeriod = 100;
            if (historicalData.Count <= warmupPeriod)
                throw new ArgumentException("Not enough data for backtesting");

            // Track last trade activity
            int lastTradeBarIndex = warmupPeriod; // Initialize to the first bar after warmup
            int maxDaysInactive = 0;

            // Calculate how many bars represent one day based on timeframe
            double barsPerDay = 24.0 * 60.0 / (int)timeFrame;

            account.StartUnixTime = historicalData[warmupPeriod].TimeStamp;

            // Pre-allocate arrays and avoid allocations in the loop
            int[] tradeIdsBuffer = new int[maxConcurrentTrades];
            int tradeIdsCount = 0;

            for (int i = warmupPeriod; i < historicalData.Count; i++)
            {
                var currentBar = historicalData[i];
                account.EndUnixTime = currentBar.TimeStamp;

                // Avoid creating a new data window each iteration
                // strategy.GetAction will receive the full history and current index

                // Handle existing positions without creating a new list
                tradeIdsCount = 0;
                foreach (var kvp in activeOrders)
                {
                    tradeIdsBuffer[tradeIdsCount++] = kvp.Key;
                    if (tradeIdsCount == maxConcurrentTrades) break;
                }

                for (int j = 0; j < tradeIdsCount; j++)
                {
                    int tradeId = tradeIdsBuffer[j];
                    var order = activeOrders[tradeId];
                    var trade = account.GetTradeById(tradeId);

                    if (trade == null) continue;

                    bool closed = false;
                    if (order.TradeType == TradeType.Long)
                    {
                        if (currentBar.Low <= trade.StopLoss)
                        {
                            account.ClosePosition(tradeId, trade.StopLoss, i);
                            closed = true;
                        }
                        else if (currentBar.High >= order.TakeProfit)
                        {
                            account.ClosePosition(tradeId, order.TakeProfit, i);
                            closed = true;
                        }
                    }
                    else if (order.TradeType == TradeType.Short)
                    {
                        if (currentBar.High >= trade.StopLoss)
                        {
                            account.ClosePosition(tradeId, trade.StopLoss, i);
                            closed = true;
                        }
                        else if (currentBar.Low <= order.TakeProfit)
                        {
                            account.ClosePosition(tradeId, order.TakeProfit, i);
                            closed = true;
                        }
                    }

                    if (closed)
                    {
                        activeOrders.Remove(tradeId);
                    }
                }

                // Calculate days of inactivity
                int currentDaysInactive = (int)Math.Floor((i - lastTradeBarIndex) / barsPerDay);
                maxDaysInactive = Math.Max(maxDaysInactive, currentDaysInactive);

                // Check if we can open new positions
                if (activeOrders.Count < maxConcurrentTrades)
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
                    }
                }

                // Update the maximum days inactive in the account
                account.MaxDaysInactive = maxDaysInactive;

                // Check if the account has been inactive for too long
                if (maxDaysInactive >= daysInactiveLimit)
                {
                    var result = account.GenerateResult();
                    backtestCache.TryAdd(paramHash, result);
                    return result;
                }
            }

            // Close any remaining positions at the end
            var lastBar = historicalData[^1];
            int lastIndex = historicalData.Count - 1;

            foreach (var tradeId in activeOrders.Keys)
            {
                account.ClosePosition(tradeId, lastBar.Close, lastIndex);
            }

            var finalResult = account.GenerateResult();
            backtestCache.TryAdd(paramHash, finalResult);
            return finalResult;
        }

        // Calculate fitness score for a backtest result
        private static double CalculateFitness(BacktestResult result)
        {
            // Avoid division by zero
            if (result.TotalTrades == 0)
                return 0;

            // Balance between profitability and win rate
            // Give higher weight to win rate for smaller sample sizes to preserve high-quality strategies
            double tradeCountFactor = Math.Min(1.0, 50.0 / result.TotalTrades); // Higher weight for fewer trades
            double winRateWeight = 200 * (0.5 + tradeCountFactor); // Win rate has more impact with fewer trades

            double profitScore = result.NetProfit;
            double winRateScore = result.WinRate * winRateWeight;
            double drawdownPenalty = result.MaxDrawdownPercent * 2;
            double inactivityPenalty = result.MaxDaysInactive * 5;

            // Additional weight for average profit per trade to favor efficient strategies
            double avgProfitPerTrade = result.NetProfit / result.TotalTrades;
            double efficiencyBonus = avgProfitPerTrade * 10;

            return profitScore + winRateScore + efficiencyBonus - drawdownPenalty - inactivityPenalty;
        }

        public void SaveFinalResult(OptimizationResult result)
        {
            // Skip if no output file specified
            if (string.IsNullOrEmpty(outputFile))
                return;

            // Only save if we have valid results
            if (result.BacktestResult.TotalTrades > minTotalTrades)
            {
                bool shouldSave = true;
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

                // Check if file exists and compare results
                if (File.Exists(outputFile))
                {
                    try
                    {
                        string previousJson = File.ReadAllText(outputFile);
                        var previousResult = JsonSerializer.Deserialize<OptimizationResult>(previousJson, jsonOptions);

                        // save if better result else do nothing
                        if (previousResult != null && previousResult.BacktestResult.NetProfit > result.BacktestResult.NetProfit)
                        {
                            Console.WriteLine($"\nPrevious result in {outputFile} has better net profit (${previousResult.BacktestResult.NetProfit:F2} vs ${result.BacktestResult.NetProfit:F2}).");
                            Console.WriteLine("Final result not saved.");
                            shouldSave = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\nError reading previous result: {ex.Message}");
                        Console.WriteLine("Will save current result.");
                    }
                }

                if (shouldSave)
                {
                    try
                    {
                        string json = JsonSerializer.Serialize(result, jsonOptions);
                        File.WriteAllText(outputFile, json);
                        Console.WriteLine($"\nFinal results saved to {outputFile}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error saving final result: {ex.Message}");
                    }
                }
            }
        }

        // Helper method to safely clone parameters
        private IndicatorCollection SafeClone(IndicatorCollection parameters)
        {
            try
            {
                return parameters.DeepClone();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Clone operation failed: {ex.Message}");
                // Create new parameters as fallback
                var fallback = new IndicatorCollection(strategyType);
                try
                {
                    // Try to copy at least some of the most important parameters
                    foreach (var indicator in parameters)
                    {
                        // Use GetType() instead of IndicatorType property for comparison
                        var matching = fallback.FirstOrDefault(i => i.GetType() == indicator.GetType());
                        if (matching != null)
                        {
                            // Just copy a few core properties without using complex operations
                            var srcParams = indicator.GetParameters();
                            var destParams = matching.GetParameters();
                            foreach (var param in srcParams.GetProperties())
                            {
                                try
                                {
                                    var destParam = destParams.GetProperties().FirstOrDefault(p => p.Name == param.Name);
                                    if (destParam != null)
                                    {
                                        destParam.Value = param.Value;
                                        destParams.UpdateFromDescriptor(destParam);
                                    }
                                }
                                catch
                                {
                                    // Ignore individual parameter copy failures
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // If even basic copying fails, we'll just use the default parameters
                }
                return fallback;
            }
        }

        // Add these helper methods for improvement tracking and termination
        private bool IsConverged()
        {
            lock (globalImprovement_lock)
            {
                convergenceCounter++;

                if (convergenceCounter % CONVERGENCE_CHECK_INTERVAL == 0 && recentFitnessValues.Count >= 10)
                {
                    double oldFitness = recentFitnessValues.Dequeue();
                    recentFitnessValues.Enqueue(globalBestFitness);

                    // If improvement is less than 0.1% over the last 10 checks, consider converged
                    return (globalBestFitness - oldFitness) / Math.Abs(oldFitness) < 0.001 &&
                           averageTemperature < 30.0;
                }

                // Maintain our window of recent fitness values
                if (recentFitnessValues.Count < 10)
                    recentFitnessValues.Enqueue(globalBestFitness);

                return false;
            }
        }

        private void UpdateExplorationRate()
        {
            // Decrease exploration slower if we're still finding improvements
            double decayRate = iterationsSinceGlobalImprovement > 500 ? 0.995 : 0.999;
            explorationRate = Math.Max(MIN_EXPLORATION_RATE, explorationRate * decayRate);
        }

        private void UpdateGlobalCooling()
        {
            // Accelerate cooling when progress stalls
            if (iterationsSinceGlobalImprovement > 500)
                globalCoolingMultiplier = Math.Min(2.0, globalCoolingMultiplier * 1.05);
        }

        private void UpdateAverageTemperature(double newTemperature)
        {
            // First, cap the incoming temperature if it exceeds our absolute max
            if (newTemperature > ABSOLUTE_MAX_TEMPERATURE)
            {
                newTemperature = ABSOLUTE_MAX_TEMPERATURE;
            }

            // Only update average with lower temperatures after we've done some exploration
            if (iterationsSinceGlobalImprovement > 200 && newTemperature > averageTemperature)
            {
                // Use a more aggressive dampening for high temperatures
                averageTemperature = averageTemperature * 0.99 + newTemperature * 0.01;
            }
            else
            {
                // Normal temperature tracking for cooling work items
                averageTemperature = averageTemperature * 0.9 + newTemperature * 0.1;
            }

            // Track the lowest temperature we've observed
            if (newTemperature < lowestObservedTemperature)
            {
                lowestObservedTemperature = newTemperature;
            }
        }

        private void CheckAndTerminate(ConcurrentQueue<WorkItem> workQueue, ManualResetEventSlim completionSignal, ref int activeWorkers)
        {
            // Check if we've gone too long without improvement, regardless of temperature
            if (iterationsSinceGlobalImprovement > MAX_ITERATIONS_WITHOUT_IMPROVEMENT)
            {
                Console.WriteLine($"Optimization stopping: No improvement after {iterationsSinceGlobalImprovement} iterations.");

                // Empty the queue
                while (workQueue.TryDequeue(out _)) { }

                // Force all workers to stop
                int currentActive = activeWorkers;
                Interlocked.Exchange(ref activeWorkers, 0);

                // Set completion signal
                completionSignal.Set();
                return;
            }

            // Terminate if temperature is too high after some exploration
            // Lower the threshold from 120 to 90
            if (iterationsSinceGlobalImprovement > 500 && averageTemperature > 90.0)
            {
                Console.WriteLine("Optimization stopping: Temperature too high without improvement.");

                // Empty the queue
                while (workQueue.TryDequeue(out _)) { }

                // Force all workers to stop
                int currentActive = activeWorkers;
                Interlocked.Exchange(ref activeWorkers, 0);

                // Set completion signal
                completionSignal.Set();
                return;
            }

            // Original convergence check
            if (IsConverged())
            {
                Console.WriteLine("Optimization stopping: Solution has converged.");

                // Empty the queue
                while (workQueue.TryDequeue(out _)) { }

                // Force all workers to stop
                int currentActive = activeWorkers;
                Interlocked.Exchange(ref activeWorkers, 0);

                // Set completion signal
                completionSignal.Set();
            }
        }

        // Add a global method to track high temperature injections properly
        private bool TryIncrementHighTempInjection()
        {
            lock (highTempLock)
            {
                if (highTempInjectionCount < MAX_HIGH_TEMP_INJECTIONS)
                {
                    highTempInjectionCount++;
                    return true;
                }
                return false;
            }
        }

        // Helper method to get a safe temperature
        private double GetSafeTemperature(double requestedTemperature)
        {
            // Apply absolute maximum cap
            return Math.Min(requestedTemperature, ABSOLUTE_MAX_TEMPERATURE);
        }

        // Add this method for saving the backtest cache
        private void SaveCacheIfNeeded()
        {
            // Skip if not enough time has passed since last save
            if ((DateTime.Now - lastCacheSaveTime).TotalSeconds < CACHE_SAVE_INTERVAL_SECONDS)
                return;

            string cacheFilePath = string.IsNullOrEmpty(outputFile)
                ? "cache_default.json"
                : $"cache_{Path.GetFileNameWithoutExtension(outputFile)}.json";

            try
            {
                // Convert the cache to a list of serializable entities
                var cacheEntries = backtestCache
                    .Select(kv => new BacktestCacheEntry
                    {
                        ParameterHash = kv.Key,
                        Result = kv.Value
                    })
                    .ToList();

                var jsonOptions = new JsonSerializerOptions { WriteIndented = false }; // No indentation to save space
                string json = JsonSerializer.Serialize(cacheEntries, jsonOptions);
                File.WriteAllText(cacheFilePath, json);

                Console.WriteLine($"Saved {cacheEntries.Count:N0} backtest results to cache file.");
                lastCacheSaveTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving backtest cache: {ex.Message}");
            }
        }

        // Add this method for loading the backtest cache
        private void LoadCacheIfExists()
        {
            string cacheFilePath = string.IsNullOrEmpty(outputFile)
                ? "cache_default.json"
                : $"cache_{Path.GetFileNameWithoutExtension(outputFile)}.json";

            if (!File.Exists(cacheFilePath))
                return;

            Console.WriteLine($"Loading backtest cache from {cacheFilePath}...");

            try
            {
                string json = File.ReadAllText(cacheFilePath);
                var jsonOptions = new JsonSerializerOptions();
                var cacheEntries = JsonSerializer.Deserialize<List<BacktestCacheEntry>>(json, jsonOptions);

                if (cacheEntries == null || cacheEntries.Count == 0)
                    return;

                int entriesAdded = 0;
                foreach (var entry in cacheEntries)
                {
                    if (backtestCache.TryAdd(entry.ParameterHash, entry.Result))
                        entriesAdded++;
                }

                Console.WriteLine($"Loaded {entriesAdded:N0} backtest results from cache file.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading backtest cache: {ex.Message}");
            }
        }
    }

    // Helper class for atomic thread-safe references
    public class AtomicReference<T> where T : struct
    {
        private T _value;
        private readonly object _lock = new();

        public AtomicReference(T initialValue)
        {
            _value = initialValue;
        }

        public T Value
        {
            get
            {
                lock (_lock)
                {
                    return _value;
                }
            }
            set
            {
                lock (_lock)
                {
                    _value = value;
                }
            }
        }
    }

    public class BacktestCacheEntry
    {
        public int ParameterHash { get; set; }
        public required BacktestResult Result { get; set; }
    }

    // Extract parameter perturbation to a separate class for better organization
    public class ParameterPerturbator
    {
        // Gets a hash code based on discretized parameter values
        public int GetDiscretizedHash(IndicatorCollection parameters)
        {
            try
            {
                // Create a discretized clone to calculate the hash
                var discretized = parameters.DeepClone();
                DiscretizeParameters(discretized);
                return discretized.GetHashCode();
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"Hash calculation error: {ex.Message}");

                // Provide a fallback hash using the original parameters
                // This might be less precise but won't crash the application
                return parameters.GetHashCode();
            }
        }

        // Discretizes parameters to reduce the number of unique combinations
        public void DiscretizeParameters(IndicatorCollection parameters)
        {
            foreach (var indicator in parameters)
            {
                var indicator_parameters = indicator.GetParameters();
                foreach (var param in indicator_parameters.GetProperties())
                {
                    if (param.Value is double dvalue)
                    {
                        // Round double values to reduce precision
                        double rounded = Math.Round(dvalue, 1);
                        param.Value = rounded;
                        indicator_parameters.UpdateFromDescriptor(param);
                    }
                    else if (param.Value is int ivalue)
                    {
                        // Integer parameters generally don't need discretization
                        // But could implement if needed: int discretized = (ivalue / INT_STEP) * INT_STEP;
                    }
                    // Boolean values are already discrete
                }
            }
        }

        // Perturb parameters randomly based on the current temperature ratio
        public static void PerturbParameters(IndicatorCollection parameters, double temperatureRatio, Random random)
        {
            try
            {
                // The higher the temperature, the more aggressive the perturbation
                foreach (var indicator in parameters)
                {
                    var indicator_parameters = indicator.GetParameters();
                    foreach (var param in indicator_parameters.GetProperties())
                    {
                        try
                        {
                            if (param.Value is double dvalue && param.Min is double dmin && param.Max is double dmax)
                            {
                                // Calculate perturbation range based on temperature
                                double range = (dmax - dmin) * temperatureRatio * 0.3;

                                // Calculate new value with random perturbation
                                double perturbation = (random.NextDouble() * 2 - 1) * range; // -range to +range
                                double newValue = dvalue + perturbation;

                                // Ensure the new value is within bounds
                                newValue = Math.Max(dmin, Math.Min(dmax, newValue));

                                // Update the parameter value
                                param.Value = newValue;
                                indicator_parameters.UpdateFromDescriptor(param);
                            }
                            else if (param.Value is int ivalue && param.Min is int imin && param.Max is int imax)
                            {
                                // Calculate perturbation range based on temperature
                                int range = (int)Math.Ceiling((imax - imin) * temperatureRatio * 0.3);
                                if (range < 1) range = 1; // Ensure at least some perturbation

                                // Calculate new value with random perturbation
                                int perturbation = random.Next(-range, range + 1);
                                int newValue = ivalue + perturbation;

                                // Ensure the new value is within bounds
                                newValue = Math.Max(imin, Math.Min(imax, newValue));

                                // Update the parameter value
                                param.Value = newValue;
                                indicator_parameters.UpdateFromDescriptor(param);
                            }
                            else if (param.Value is bool value)
                            {
                                // Flip boolean with a probability based on temperature
                                if (random.NextDouble() < temperatureRatio * 0.3)
                                {
                                    param.Value = !value;
                                    indicator_parameters.UpdateFromDescriptor(param);
                                }
                            }
                            else
                            {
                                // Skip unsupported parameter types instead of throwing an exception
                                Console.WriteLine($"Skipping unsupported parameter type: {param.Value?.GetType().Name ?? "null"}");
                            }
                        }
                        catch (Exception paramEx)
                        {
                            // If a single parameter fails, log it but continue with other parameters
                            Console.WriteLine($"Error perturbing parameter: {paramEx.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // If the entire perturbation fails, log it
                Console.WriteLine($"Error during parameter perturbation: {ex.Message}");
                // Continue without perturbation
            }
        }
    }
}