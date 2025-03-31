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
        string dataFilePath,
        StrategyType strategyType,
        TimeFrame timeFrame = TimeFrame.OneHour,
        double initialBalance = 1000,
        double feePercentage = 0.05,
        int lookbackDays = 1500,
        int threadCount = -1,
        double minTemperature = 0.05,
        double defaultCoolingRate = 0.95,
        int maxConcurrentTrades = 5,
        double maxDrawdownPercent = 30,
        int leverage = 10,
        int daysInactiveLimit = 30,
        double minWinRatePercent = 0.5,
        string outputFile = ""
        )
    {
        private readonly string dataFilePath = dataFilePath;
        private readonly StrategyType strategyType = strategyType;
        private readonly TimeFrame timeFrame = timeFrame;
        private readonly double initialBalance = initialBalance;
        private readonly double feePercentage = feePercentage;
        private readonly int maxConcurrentTrades = maxConcurrentTrades;
        private readonly double maxDrawdownPercent = maxDrawdownPercent;
        private readonly int leverage = leverage;
        private readonly int daysInactiveLimit = daysInactiveLimit;
        private readonly double minWinRatePercent = minWinRatePercent;
        private readonly int minTotalTrades = lookbackDays / 5;
        private readonly double minTemperature = minTemperature;
        private readonly double defaultCoolingRate = defaultCoolingRate;
        private readonly string outputFile = outputFile;
        private readonly ConcurrentBag<(IndicatorCollection Params, BacktestResult Result)> candidates = [];

        private readonly ConcurrentQueue<(IndicatorCollection Params, BacktestResult Result, double Fitness)> results = [];

        // Track both tested hashes and a cache of backtests for similar parameter sets
        private readonly ConcurrentDictionary<int, byte> testedParameterHashes = new();
        private readonly ConcurrentDictionary<int, BacktestResult> backtestCache = new();
        private readonly ConcurrentDictionary<int, IndicatorCollection> parameterCache = new();
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
        private const int MAX_ITERATIONS_WITHOUT_IMPROVEMENT = 10000;  // Terminate if no improvement for this many iterations
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

        // Add this field to track the full historical data
        private List<BarData>? historicalData;

        // Add file locking object at the top with other private fields
        private readonly object cacheSaveLock = new();

        /// <summary>
        /// Find optimal parameters for the strategy using simulated annealing with work stealing.
        /// </summary>
        public OptimizationResult FindOptimalParameters()
        {
            return ProgressiveOptimization();
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

        private void FindSeedCandidate(List<BarData> timeframeData)
        {
            var parameterPerturbator = new ParameterPerturbator();
            int searchAttempts = 0;
            var random = new Random(Guid.NewGuid().GetHashCode());

            // Add variables to track cache saving during seed search
            int lastCacheSaveAttempt = 0;
            const int SEED_CACHE_SAVE_FREQUENCY = 250; // Changed from 50 to 250 attempts

            // Keep searching until we find a good candidate
            while (candidates.IsEmpty)
            {
                searchAttempts++;

                // Save cache more frequently during seed search
                if (searchAttempts - lastCacheSaveAttempt >= SEED_CACHE_SAVE_FREQUENCY)
                {
                    SaveCacheIfNeeded(true); // Force save during seed search
                    lastCacheSaveAttempt = searchAttempts;
                }

                // Create smarter parameter sets as search progresses
                var candidateParams = parameterPerturbator.GenerateSmartCandidate(strategyType, searchAttempts, random);

                // Discretize and check if we've already tested this parameter set
                int discretizedHash = parameterPerturbator.GetDiscretizedHash(candidateParams);
                if (testedParameterHashes.ContainsKey(discretizedHash))
                {
                    // Try to use cache instead of skipping completely
                    if (backtestCache.TryGetValue(discretizedHash, out var cachedResult))
                    {
                        var relaxedCriteria_cached = searchAttempts < 20 &&
                                              cachedResult.TotalTrades > minTotalTrades / 2 &&
                                              cachedResult.MaxDrawdownPercent <= maxDrawdownPercent * 1.5 &&
                                              cachedResult.WinRate >= minWinRatePercent * 0.8;
                        // Evaluate the cached result without running a new backtest
                        if ((cachedResult.TotalTrades > minTotalTrades &&
                            cachedResult.MaxDrawdownPercent <= maxDrawdownPercent &&
                            cachedResult.WinRate >= minWinRatePercent) || relaxedCriteria_cached)
                        {
                            candidates.Add((candidateParams, cachedResult));
                            SaveCacheIfNeeded();
                            return;
                        }
                    }
                    continue; // Skip testing this parameter set
                }

                // Mark this parameter set as tested
                testedParameterHashes.TryAdd(discretizedHash, 0);

                // Evaluate the candidate
                var candidateStrategy = StrategyFactory.CreateStrategy(strategyType, candidateParams);
                var candidateResult = RunBacktest(candidateStrategy, timeframeData, discretizedHash, true); // Allow early termination

                // For very early iterations, use a relaxed criteria to find a promising region
                bool relaxedCriteria = searchAttempts < 20 &&
                                       candidateResult.TotalTrades > minTotalTrades / 2 &&
                                       candidateResult.MaxDrawdownPercent <= maxDrawdownPercent * 1.5 &&
                                       candidateResult.WinRate >= minWinRatePercent * 0.8;

                // Check if this candidate meets our criteria - with a more lenient drawdown limit
                if ((candidateResult.TotalTrades > minTotalTrades &&
                    candidateResult.MaxDrawdownPercent <= maxDrawdownPercent &&
                    candidateResult.WinRate >= minWinRatePercent) || relaxedCriteria)
                {
                    Console.WriteLine($"Found a good seed candidate with fitness: {CalculateFitness(candidateResult):F2}, win rate: {candidateResult.WinRatePercent:F2}%, drawdown: {candidateResult.MaxDrawdownPercent:F2}%");
                    // Take a snapshot of the current candidates
                    var snapshot = candidates.ToArray();

                    // If the bag is empty or our candidate is better than existing ones, add it
                    if (snapshot.Length == 0)
                    {
                        candidates.Add((candidateParams, candidateResult));
                        SaveCacheIfNeeded();
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
                        SaveCacheIfNeeded();
                        return;
                    }
                }
            }
        }

        // Add this method to avoid excessive cloning
        private static bool AreParametersSimilar(IndicatorCollection params1, IndicatorCollection params2, double threshold = 0.05)
        {
            // Quick check for identity
            if (ReferenceEquals(params1, params2))
                return true;

            // Check if parameters are close enough to consider them similar
            foreach (var indicator1 in params1)
            {
                var indicator2 = params2.FirstOrDefault(i => i.GetType() == indicator1.GetType());
                if (indicator2 == null)
                    return false;

                // Compare parameters
                var params1Dict = indicator1.GetParameters().GetProperties()
                    .ToDictionary(p => p.Name, p => p.Value);

                var params2Dict = indicator2.GetParameters().GetProperties()
                    .ToDictionary(p => p.Name, p => p.Value);

                foreach (var kvp in params1Dict)
                {
                    if (!params2Dict.TryGetValue(kvp.Key, out var value2))
                        return false;

                    if (kvp.Value is double d1 && value2 is double d2)
                    {
                        // Use relative difference for double values
                        if (Math.Abs(d1) > 0.00001)
                        {
                            if (Math.Abs(d1 - d2) / Math.Abs(d1) > threshold)
                                return false;
                        }
                        else if (Math.Abs(d1 - d2) > threshold) // Absolute comparison for small values
                        {
                            return false;
                        }
                    }
                    else if (kvp.Value is int i1 && value2 is int i2)
                    {
                        // Use relative difference threshold for integer parameters too
                        if (i1 != 0)
                        {
                            if (Math.Abs((double)(i1 - i2) / i1) > threshold)
                                return false;
                        }
                        else if (i2 != 0) // Special case for zeros
                        {
                            return false;
                        }
                    }
                    else if (!Equals(kvp.Value, value2))
                    {
                        return false;
                    }
                }
            }

            return true;
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
        private BacktestResult RunBacktest(ITradingStrategy strategy, List<BarData> historicalData, int paramHash, bool allowEarlyTermination = true)
        {
            // Check if we've cached this result
            if (backtestCache.TryGetValue(paramHash, out var cachedResult))
            {
                Interlocked.Increment(ref cachedBacktestsUsed);
                return cachedResult;
            }

            // If no exact match, check for similar parameter sets
            // This can save time by using an approximate result when parameters are very close

            // Store parameter hash for future similarity checks
            if (!testedParameterHashes.ContainsKey(paramHash))
            {
                testedParameterHashes.TryAdd(paramHash, 0);
            }

            // Look for similar parameter sets in the cache
            // We can only check for similarity with parameters we already have cached
            if (paramHash != 0 && !testedParameterHashes.ContainsKey(paramHash))
            {
                // We don't have direct access to strategy parameters
                // Instead, we'll use the StrategyFactory's LastCreatedParameters if available
                if (StrategyFactory.LastCreatedParameters != null)
                {
                    // Store for future similarity checks
                    parameterCache.TryAdd(paramHash, StrategyFactory.LastCreatedParameters.DeepClone());

                    // Look for similar parameter sets in the cache
                    foreach (var entry in backtestCache)
                    {
                        // Get parameters from the cache entry 
                        if (parameterCache.TryGetValue(entry.Key, out var cachedParams) &&
                            AreParametersSimilar(StrategyFactory.LastCreatedParameters, cachedParams, 0.02)) // Use tighter threshold
                        {
                            Interlocked.Increment(ref cachedBacktestsUsed);
                            return entry.Value;
                        }
                    }
                }
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

            // Thresholds for early termination
            const int MinBarsForTermination = 200; // Don't terminate too early
            const double DrawdownTerminationThreshold = 0.15; // Terminate if drawdown exceeds 15%
            const double InactivityTerminationThreshold = 0.3; // Terminate if 30% of the time had no trades
            const int MinTradesBeforeTermination = 5; // Need at least this many trades to evaluate

            // Track performance metrics for early termination
            int consecutiveLosses = 0;
            int totalTrades = 0;
            double peakBalance = initialBalance;

            for (int i = warmupPeriod; i < historicalData.Count; i++)
            {
                var currentBar = historicalData[i];
                account.EndUnixTime = currentBar.TimeStamp;

                // Avoid creating a new data window each iteration
                // strategy.GetAction will receive the full history and current index

                // Handle existing positions without creating a new list
                int tradeIdsCount = 0;
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
                            consecutiveLosses++;
                            totalTrades++;
                        }
                        else if (currentBar.High >= order.TakeProfit)
                        {
                            account.ClosePosition(tradeId, order.TakeProfit, i);
                            closed = true;
                            consecutiveLosses = 0;
                            totalTrades++;
                        }
                    }
                    else if (order.TradeType == TradeType.Short)
                    {
                        if (currentBar.High >= trade.StopLoss)
                        {
                            account.ClosePosition(tradeId, trade.StopLoss, i);
                            closed = true;
                            consecutiveLosses++;
                            totalTrades++;
                        }
                        else if (currentBar.Low <= order.TakeProfit)
                        {
                            account.ClosePosition(tradeId, order.TakeProfit, i);
                            closed = true;
                            consecutiveLosses = 0;
                            totalTrades++;
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

                // Calculate current drawdown
                peakBalance = Math.Max(peakBalance, account.CurrentBalance);
                double currentDrawdown = (peakBalance - account.CurrentBalance) / peakBalance;

                // Early termination check for obviously poor strategies
                if (allowEarlyTermination && i > warmupPeriod + MinBarsForTermination)
                {
                    // Check for excessive drawdown
                    if (currentDrawdown > DrawdownTerminationThreshold)
                    {
                        var earlyResult = account.GenerateResult();
                        earlyResult.TerminatedEarly = true;
                        backtestCache.TryAdd(paramHash, earlyResult);
                        return earlyResult;
                    }

                    // Check for excessive inactivity
                    double inactivityRatio = (double)maxDaysInactive * barsPerDay / (i - warmupPeriod);
                    if (inactivityRatio > InactivityTerminationThreshold && totalTrades >= MinTradesBeforeTermination)
                    {
                        var earlyResult = account.GenerateResult();
                        earlyResult.TerminatedEarly = true;
                        backtestCache.TryAdd(paramHash, earlyResult);
                        return earlyResult;
                    }

                    // Check for too many consecutive losses
                    if (consecutiveLosses >= 5 && totalTrades >= MinTradesBeforeTermination)
                    {
                        var earlyResult = account.GenerateResult();
                        earlyResult.TerminatedEarly = true;
                        backtestCache.TryAdd(paramHash, earlyResult);
                        return earlyResult;
                    }
                }

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
        private double CalculateFitness(BacktestResult result)
        {
            // Avoid division by zero
            if (result.TotalTrades == 0)
                return 0;

            // Trade count factors - strongly penalize strategies with fewer than minTotalTrades trades
            double tradeCountPenalty = 0;
            if (result.TotalTrades < minTotalTrades)
            {
                // Exponential penalty that gets more severe as trade count gets lower
                tradeCountPenalty = 500 * Math.Exp(-0.15 * result.TotalTrades);
            }

            // Prioritize net profit with a higher constant multiplier
            double profitScore = result.NetProfit * 1.5;

            double winRateScore = result.WinRate * 100;

            double drawdownPenalty = result.MaxDrawdownPercent * 2;
            double inactivityPenalty = result.MaxDaysInactive * 5;

            double avgProfitPerTrade = result.NetProfit / result.TotalTrades;
            // double efficiencyBonus = Math.Min(avgProfitPerTrade * 10, result.NetProfit * 0.5);

            return profitScore + winRateScore - drawdownPenalty - inactivityPenalty - tradeCountPenalty;
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
            // Increase max iterations without improvement based on exploration rate
            int adaptedMaxIterations = (int)(MAX_ITERATIONS_WITHOUT_IMPROVEMENT * (1.0 + explorationRate));
            if (iterationsSinceGlobalImprovement > adaptedMaxIterations)
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

            // Terminate if temperature is too high after some exploration but be more permissive
            if (iterationsSinceGlobalImprovement > 800 && averageTemperature > 95.0)
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
            if (IsConverged() && iterationsSinceGlobalImprovement > 500) // Only check convergence after 500 iterations
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
        private static double GetSafeTemperature(double requestedTemperature)
        {
            // Apply absolute maximum cap
            return Math.Min(requestedTemperature, ABSOLUTE_MAX_TEMPERATURE);
        }

        private void SaveCacheIfNeeded(bool forceSave = false)
        {
            // Skip if not enough time has passed since last save and not forced
            if (!forceSave && (DateTime.Now - lastCacheSaveTime).TotalSeconds < CACHE_SAVE_INTERVAL_SECONDS)
                return;

            // Skip if cache is too small to be worth saving
            if (backtestCache.Count < 10)
                return;

            // Use lock to prevent multiple threads from saving simultaneously
            if (!Monitor.TryEnter(cacheSaveLock, 100))
            {
                // If we can't get the lock within 100ms, just skip this save operation
                return;
            }

            try
            {
                // Check time again after getting lock (another thread might have saved already)
                if (!forceSave && (DateTime.Now - lastCacheSaveTime).TotalSeconds < CACHE_SAVE_INTERVAL_SECONDS)
                    return;

                string cacheFilePath = string.IsNullOrEmpty(outputFile)
                    ? "cache_default.json"
                    : $"cache_{Path.GetFileNameWithoutExtension(outputFile)}.json";

                // Use a temporary file to avoid locking issues
                string tempFilePath = $"{cacheFilePath}.temp";

                try
                {
                    // Convert the cache to a list of serializable entities
                    var cacheEntries = backtestCache
                        .Select(kv => new BacktestCacheEntry
                        {
                            ParameterHash = kv.Key,
                            Result = kv.Value,
                            // Include parameters if available
                            Parameters = parameterCache.TryGetValue(kv.Key, out var parameters)
                                ? parameters.ToDto()
                                : null
                        })
                        .ToList();

                    var jsonOptions = new JsonSerializerOptions { WriteIndented = false }; // No indentation to save space
                    string json = JsonSerializer.Serialize(cacheEntries, jsonOptions);

                    // Write to temp file first
                    File.WriteAllText(tempFilePath, json);

                    // Then move the temp file to the target file (atomic operation)
                    try
                    {
                        // Try to delete the target file if it exists
                        if (File.Exists(cacheFilePath))
                        {
                            File.Delete(cacheFilePath);
                        }

                        // Move the temp file to the target file
                        File.Move(tempFilePath, cacheFilePath);
                    }
                    catch (IOException)
                    {
                        // If we can't move the file, it might be because another process has it open
                        // In this case, we'll just keep the temp file for next time
                        Console.WriteLine("Warning: Cache saved to temporary file only.");
                    }

                    // Only log if we're not in a seed search loop (to reduce console spam)
                    if (!forceSave || cacheEntries.Count % 500 == 0)
                    {
                        Console.WriteLine($"Saved {cacheEntries.Count:N0} backtest results to cache file.");
                    }

                    lastCacheSaveTime = DateTime.Now;
                }
                catch (Exception ex)
                {
                    // Log error instead of silently handling it
                    Console.WriteLine($"Error saving cache: {ex.Message}");
                }
                finally
                {
                    // Clean up temp file if it still exists
                    try
                    {
                        if (File.Exists(tempFilePath))
                        {
                            File.Delete(tempFilePath);
                        }
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
            finally
            {
                // Always release the lock
                Monitor.Exit(cacheSaveLock);
            }
        }

        private void LoadCacheIfExists()
        {
            string cacheFilePath = string.IsNullOrEmpty(outputFile)
                ? "cache_default.json"
                : $"cache_{Path.GetFileNameWithoutExtension(outputFile)}.json";

            // Also check for temp file from previous runs
            string tempFilePath = $"{cacheFilePath}.temp";

            if (!File.Exists(cacheFilePath) && !File.Exists(tempFilePath))
                return;

            // Use the temp file if it exists and the main file doesn't, or if the temp file is newer
            string fileToLoad = cacheFilePath;
            if (File.Exists(tempFilePath))
            {
                if (!File.Exists(cacheFilePath) ||
                    File.GetLastWriteTime(tempFilePath) > File.GetLastWriteTime(cacheFilePath))
                {
                    fileToLoad = tempFilePath;
                    Console.WriteLine("Loading cache from temporary file (previous save was incomplete)");
                }
            }

            Console.WriteLine($"Loading backtest cache from {fileToLoad}...");

            try
            {
                // Limit the number of concurrent threads during loading to avoid
                // too many threads being created just for deserialization
                int concurrentLimit = Math.Min(Environment.ProcessorCount, 4);

                string json = File.ReadAllText(fileToLoad);
                var jsonOptions = new JsonSerializerOptions();
                var cacheEntries = JsonSerializer.Deserialize<List<BacktestCacheEntry>>(json, jsonOptions);

                if (cacheEntries == null || cacheEntries.Count == 0)
                    return;

                // Process entries in parallel with a reasonable degree of parallelism
                Parallel.ForEach(
                    cacheEntries,
                    new ParallelOptions { MaxDegreeOfParallelism = concurrentLimit },
                    entry =>
                    {
                        backtestCache.TryAdd(entry.ParameterHash, entry.Result);

                        // Also add to parameter cache if parameters are available
                        if (entry.Parameters != null)
                        {
                            try
                            {
                                var stratParams = new IndicatorCollection(strategyType);
                                stratParams.FromDto(entry.Parameters);
                                parameterCache.TryAdd(entry.ParameterHash, stratParams);
                            }
                            catch
                            {
                                // Ignore parameter loading errors for individual entries
                            }
                        }

                        // Mark as tested
                        testedParameterHashes.TryAdd(entry.ParameterHash, 0);
                    });

                Console.WriteLine($"Loaded {backtestCache.Count:N0} backtest results and {parameterCache.Count:N0} parameter sets from cache file.");
            }
            catch (Exception ex)
            {
                // Log the error instead of silently ignoring it
                Console.WriteLine($"Error loading cache: {ex.Message}");
            }
        }

        private static List<BarData> CreateDownsampledData(List<BarData> fullData, double factor)
        {
            if (factor <= 1) return fullData;

            // Calculate how many bars to sample
            int resultSize = Math.Max(500, (int)(fullData.Count / factor));

            if (resultSize >= fullData.Count)
                return fullData;

            // Use systematic sampling for efficiency and to maintain the time series nature
            // This preserves big trends while reducing data size
            var result = new List<BarData>(resultSize);
            double step = (double)fullData.Count / resultSize;

            for (double i = 0; i < fullData.Count; i += step)
            {
                int index = (int)i;
                result.Add(fullData[index]);
            }

            // Always include some recent data for more accurate results
            int recentDataCount = Math.Min(100, fullData.Count / 10);
            int startIdx = fullData.Count - recentDataCount;

            // Add recent data (maybe with some overlap, which is fine)
            for (int i = startIdx; i < fullData.Count; i++)
            {
                // Only add if not too close to an existing point to avoid duplicates
                if (!result.Contains(fullData[i]))
                {
                    result.Add(fullData[i]);
                }
            }

            // Sort by timestamp to ensure chronological order after adding recent data
            result.Sort((a, b) => a.TimeStamp.CompareTo(b.TimeStamp));

            Console.WriteLine($"Created downsampled dataset with {result.Count} bars (from original {fullData.Count})");
            return result;
        }

        public OptimizationResult ProgressiveOptimization()
        {
            totalStopwatch.Start();

            // Load any existing backtest cache
            LoadCacheIfExists();

            var loadedData = CsvDataUtility.LoadBarDataFromCsv(dataFilePath);

            // Start the optimization process
            Console.WriteLine($"\nRunning progressive optimization for {strategyType} strategy on {TimeFrameUtility.GetTimeFrameDisplayName(timeFrame)} timeframe.");
            Console.WriteLine($"Initial balance: ${initialBalance}, Fee: {feePercentage}%");
            Console.WriteLine();

            // Trim the historical data to the lookback period
            loadedData = [.. loadedData.Skip(loadedData.Count - lookbackDays * 24 * 60)];
            Console.WriteLine($"Loaded {loadedData.Count:N0} bars of historical data.");

            // Convert the data to the desired timeframe
            this.historicalData = TimeFrameUtility.ConvertTimeFrame(loadedData, timeFrame);

            // Configure thread count
            threadCount = threadCount == -1 ? Environment.ProcessorCount - 1 : threadCount == 0 ? Environment.ProcessorCount : threadCount;
            if (threadCount > Environment.ProcessorCount) threadCount = Environment.ProcessorCount;

            // PHASE 1: Quick exploration with 50% of data
            Console.WriteLine("\n=== PHASE 1: Initial Parameter Exploration ===");
            Console.WriteLine("Using 50% of data for quick parameter exploration...");
            var downsampledData = CreateDownsampledData(historicalData, 2);
            double phase1Temperature = minTemperature * 3; // Higher temperature for broader exploration
            OptimizationResult phase1Result = RunOptimizationPhase(downsampledData, phase1Temperature);
            Console.WriteLine($"Phase 1 complete - Best fitness: {CalculateFitness(phase1Result.BacktestResult):F2}");

            // PHASE 2: Refined exploration with 50% of data
            Console.WriteLine("\n=== PHASE 2: Refined Parameter Exploration ===");
            Console.WriteLine("Using 75% of data with Phase 1 best parameters...");
            var mediumSampleData = CreateDownsampledData(historicalData, 1.5);
            double phase2Temperature = minTemperature * 2; // Medium temperature for balanced search
            OptimizationResult phase2Result = RunOptimizationPhase(mediumSampleData, phase2Temperature, phase1Result.BestParameters);
            Console.WriteLine($"Phase 2 complete - Best fitness: {CalculateFitness(phase2Result.BacktestResult):F2}");

            // PHASE 3: Final optimization with full data
            Console.WriteLine("\n=== PHASE 3: Final Parameter Optimization ===");
            Console.WriteLine("Using 100% of data with Phase 2 best parameters...");
            OptimizationResult finalResult = RunOptimizationPhase(historicalData, minTemperature, phase2Result.BestParameters);
            Console.WriteLine($"Phase 3 complete - Best fitness: {CalculateFitness(finalResult.BacktestResult):F2}");

            // Summary of all phases
            Console.WriteLine("\n=== PROGRESSIVE OPTIMIZATION COMPLETE ===");
            Console.WriteLine($"Total backtests run: {totalBacktestsRun:N0}, cached hits: {cachedBacktestsUsed:N0}");
            Console.WriteLine($"Cache hit rate: {cachedBacktestsUsed * 100.0 / (totalBacktestsRun + cachedBacktestsUsed):F1}%");
            Console.WriteLine($"Total optimization time: {totalStopwatch.Elapsed.TotalMinutes:F1} minutes");

            totalStopwatch.Stop();
            SaveCacheIfNeeded(); // Save the final cache before exiting

            return finalResult;
        }

        // Add this helper method for running a single optimization phase
        private OptimizationResult RunOptimizationPhase(List<BarData> phaseData, double phaseTemperature, IndicatorParameterDto[]? seedParameters = null)
        {
            // Determine if this is the final phase by comparing with original data size
            bool isFinalPhase = phaseData.Count >= (historicalData?.Count ?? int.MaxValue);

            // Create a shared work queue
            var phaseWorkQueue = new ConcurrentQueue<WorkItem>();

            // Initialize a single parameterPerturbator instance for the entire method
            var methodPerturbator = new ParameterPerturbator();

            // Initialize with seed parameters if provided, otherwise start with random
            List<IndicatorCollection> candidateList = new();

            if (seedParameters != null && seedParameters.Length > 0)
            {
                Console.WriteLine("Using seed parameters from previous phase...");
                var seedCollection = new IndicatorCollection(strategyType);
                seedCollection.FromDto(seedParameters);

                // Add the seed collection to our candidate list
                candidateList.Add(seedCollection.DeepClone());

                // Also add variations of the seed to increase diversity
                var random = new Random();
                for (int i = 0; i < Math.Min(threadCount, 5); i++)
                {
                    var variantParams = seedCollection.DeepClone();
                    // Increase perturbation for more diversity (0.2 to 0.5)
                    ParameterPerturbator.PerturbParameters(variantParams, 0.3 + (i * 0.1), random);
                    candidateList.Add(variantParams);
                }

                // Add some completely random parameters to prevent getting stuck
                for (int i = 0; i < Math.Min(threadCount, 3); i++)
                {
                    var randomParams = new IndicatorCollection(strategyType);
                    randomParams.RandomizeParameters();
                    candidateList.Add(randomParams);
                }

                // Use our optimized work distribution
                DistributeWorkEvenly(candidateList, phaseWorkQueue, threadCount, phaseTemperature);
            }
            else
            {
                Console.WriteLine("Searching for initial seed candidates...");

                // Find initial candidates in parallel
                candidates.Clear(); // Clear any previous candidates
                var candidateTasks = new List<Task>();
                for (int i = 0; i < threadCount; i++)
                {
                    candidateTasks.Add(Task.Run(() => FindSeedCandidate(phaseData)));
                }

                // Wait for at least one thread to find a good candidate, or until all tasks complete
                while (candidates.IsEmpty && !Task.WhenAll([.. candidateTasks]).IsCompleted)
                {
                    Thread.Sleep(100);
                }

                // Wait for all tasks to complete
                Task.WaitAll([.. candidateTasks]);

                if (candidates.Count == 0)
                {
                    Console.WriteLine("No valid candidates found. Generating random starting points...");
                    for (int i = 0; i < threadCount * 5; i++)
                    {
                        var random = new Random(Guid.NewGuid().GetHashCode());
                        var candidateParams = methodPerturbator.GenerateSmartCandidate(strategyType, i, random);
                        candidateList.Add(candidateParams);
                    }

                    // Use our optimized work distribution
                    DistributeWorkEvenly(candidateList, phaseWorkQueue, threadCount, phaseTemperature);
                }
                else
                {
                    // Use the candidates found
                    foreach (var candidate in candidates)
                    {
                        candidateList.Add(candidate.Params.DeepClone());
                    }

                    // Use our optimized work distribution
                    DistributeWorkEvenly(candidateList, phaseWorkQueue, threadCount, phaseTemperature);
                }
            }

            // Run the optimization with the prepared work queue
            var completionSignal = new ManualResetEventSlim(false);
            int activeWorkers = threadCount;
            results.Clear(); // Clear previous results for this phase

            // Best result for this phase
            var phaseBestResult = new AtomicReference<(IndicatorCollection Params, BacktestResult Result, double Fitness)>(
                (new IndicatorCollection(strategyType), new BacktestResult(), double.MinValue));

            // Progress reporter for this phase
            using var progressReporter = new Timer(_ => ReportProgress(phaseBestResult), null, 5000, 5000);

            // Start worker tasks
            var parameterPerturbator = new ParameterPerturbator();
            var workerTasks = new List<Task>();
            for (int i = 0; i < threadCount; i++)
            {
                int workerId = i;
                workerTasks.Add(Task.Run(() =>
                {
                    ProcessWorkQueue(
                        phaseData,
                        phaseWorkQueue,
                        workerId,
                        completionSignal,
                        ref activeWorkers,
                        phaseBestResult,
                        parameterPerturbator);
                }));
            }

            // Wait for completion
            completionSignal.Wait();

            // Process results
            var allResults = results.OrderByDescending(r => r.Fitness).ToList();

            // Use different trade requirements based on phase
            int phaseMinTrades = isFinalPhase ? minTotalTrades : minTotalTrades / 2;

            // Filter results more strictly in final phase (full data)
            var validResults = allResults.Where(r =>
                r.Result.TotalTrades >= phaseMinTrades &&
                r.Result.MaxDrawdownPercent <= maxDrawdownPercent).ToList();

            if (validResults.Count == 0)
            {
                // Try with a more relaxed criteria if we don't have valid results
                phaseMinTrades = Math.Max(5, phaseMinTrades / 2);
                Console.WriteLine($"No valid results found. Relaxing minimum trades to {phaseMinTrades}.");

                validResults = allResults.Where(r =>
                    r.Result.TotalTrades >= phaseMinTrades &&
                    r.Result.MaxDrawdownPercent <= maxDrawdownPercent * 1.25).ToList();

                if (validResults.Count == 0 && allResults.Count > 0)
                {
                    // If we still have no valid results, just take the top 5 by fitness and pick the one with most trades
                    Console.WriteLine("Still no valid results. Selecting best available strategy.");
                    var topResults = allResults.Take(Math.Min(5, allResults.Count)).ToList();
                    validResults = [topResults.OrderByDescending(r => r.Result.TotalTrades).First()];
                }
            }

            if (validResults.Count == 0)
            {
                if (allResults.Count > 0)
                {
                    // Return best of what we have
                    var (Params, Result, _) = allResults.First();
                    return new OptimizationResult
                    {
                        BestParameters = Params.ToDto(),
                        BacktestResult = Result
                    };
                }
                else
                {
                    // No results at all
                    return new OptimizationResult
                    {
                        BestParameters = new IndicatorCollection(strategyType).ToDto(),
                        BacktestResult = new BacktestResult()
                    };
                }
            }

            // First prioritize results with sufficient trades
            var preferredResults = validResults.Where(r => r.Result.TotalTrades >= phaseMinTrades).ToList();

            // If we have results with sufficient trades, use those; otherwise use the best valid result
            var (bestParameters, finalResult, _) = preferredResults.Count > 0
                ? preferredResults.First()
                : validResults.First();

            return new OptimizationResult
            {
                BestParameters = bestParameters.ToDto(),
                BacktestResult = finalResult
            };
        }

        // Add this method after the AreParametersSimilar method
        private void DistributeWorkEvenly(List<IndicatorCollection> candidates, ConcurrentQueue<WorkItem> workQueue, int threadCount, double temperature)
        {
            // Create distinct types of work items
            var explorationItems = new List<WorkItem>(); // New random parameters
            var exploitationItems = new List<WorkItem>(); // Refinement of good parameters
            var diversificationItems = new List<WorkItem>(); // Parameter space coverage

            var parameterPerturbator = new ParameterPerturbator();
            var random = new Random();

            // Create a balanced mix of different search strategies

            // 1. Exploration items - completely new parameter sets
            // Increase from threadCount*2 to threadCount*3 for more exploration
            for (int i = 0; i < threadCount * 3; i++)
            {
                var explorationParams = parameterPerturbator.GenerateSmartCandidate(strategyType, i, random);

                explorationItems.Add(new WorkItem
                {
                    Parameters = explorationParams,
                    Temperature = temperature * 0.9, // Increase from 0.8 to 0.9 for broader exploration
                    PreviousBestWinRate = 0,
                    IterationsSinceImprovement = 0,
                    TotalIterations = 0,
                    AdaptiveCoolingRate = defaultCoolingRate,
                });
            }

            // 2. Exploitation items - variations of the best candidates
            // Take more candidate variations
            int variationsPerCandidate = Math.Max(2, threadCount / 2);
            foreach (var candidate in candidates.Take(Math.Min(candidates.Count, 5))) // Take up to 5 candidates
            {
                // Create multiple variations with decreasing perturbation amounts
                for (int i = 0; i < variationsPerCandidate; i++)
                {
                    // Use a wider range of perturbation amounts (from 0.4 down to 0.05)
                    double perturbAmount = 0.4 * (1.0 - i / (double)variationsPerCandidate);
                    var exploitParams = candidate.DeepClone();
                    ParameterPerturbator.PerturbParameters(exploitParams, perturbAmount, random);

                    exploitationItems.Add(new WorkItem
                    {
                        Parameters = exploitParams,
                        Temperature = temperature * 0.7, // Increase from 0.6 to 0.7 to allow more movement
                        PreviousBestWinRate = 0,
                        IterationsSinceImprovement = 0,
                        TotalIterations = 0,
                        AdaptiveCoolingRate = defaultCoolingRate,
                    });
                }
            }

            // 3. Diversification items - designed for coverage of parameter space
            // Increase segment count to ensure better parameter space coverage
            int segmentCount = threadCount * 3;
            for (int i = 0; i < segmentCount; i++)
            {
                var diverseParams = parameterPerturbator.GenerateSmartCandidate(strategyType, i + 30, random); // Start at iteration 30

                diversificationItems.Add(new WorkItem
                {
                    Parameters = diverseParams,
                    Temperature = temperature * 0.8, // Increase from 0.7 to 0.8 for more diversity
                    PreviousBestWinRate = 0,
                    IterationsSinceImprovement = 0,
                    TotalIterations = 0,
                    AdaptiveCoolingRate = defaultCoolingRate,
                });
            }

            // Now distribute the work items evenly among threads
            // Each thread should get a mix of different types

            // Calculate items per thread
            int explorationsPerThread = Math.Max(1, explorationItems.Count / threadCount);
            int exploitationsPerThread = Math.Max(1, exploitationItems.Count / threadCount);
            int diversificationsPerThread = Math.Max(1, diversificationItems.Count / threadCount);

            // Distribute each type
            for (int i = 0; i < threadCount; i++)
            {
                // Add exploration items
                foreach (var item in explorationItems
                    .Skip(i * explorationsPerThread)
                    .Take(explorationsPerThread))
                {
                    workQueue.Enqueue(item);
                }

                // Add exploitation items
                foreach (var item in exploitationItems
                    .Skip(i * exploitationsPerThread)
                    .Take(exploitationsPerThread))
                {
                    workQueue.Enqueue(item);
                }

                // Add diversification items
                foreach (var item in diversificationItems
                    .Skip(i * diversificationsPerThread)
                    .Take(diversificationsPerThread))
                {
                    workQueue.Enqueue(item);
                }
            }

            // Add any remaining items
            foreach (var item in explorationItems.Skip(threadCount * explorationsPerThread))
                workQueue.Enqueue(item);

            foreach (var item in exploitationItems.Skip(threadCount * exploitationsPerThread))
                workQueue.Enqueue(item);

            foreach (var item in diversificationItems.Skip(threadCount * diversificationsPerThread))
                workQueue.Enqueue(item);

            Console.WriteLine($"Distributed {workQueue.Count} work items: {explorationItems.Count} exploration, {exploitationItems.Count} exploitation, {diversificationItems.Count} diversification");
        }

        // Helper method to try to extract parameters from a strategy
        private IndicatorCollection GetStrategyParameters(ITradingStrategy strategy)
        {
            // For strategies we create, we'll have the parameters available in the parameterCache
            if (strategy is TradingStrategyBase && StrategyFactory.LastCreatedParameters != null)
            {
                return StrategyFactory.LastCreatedParameters;
            }

            // Return an empty collection as fallback
            return new IndicatorCollection(strategyType);
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
        public IndicatorParameterDto[]? Parameters { get; set; }
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

        // Add this method after the DiscretizeParameters method in the ParameterPerturbator class
        public IndicatorCollection GenerateSmartCandidate(StrategyType strategyType, int attempt, Random random)
        {
            var candidateParams = new IndicatorCollection(strategyType);

            if (attempt < 10)
            {
                // First few attempts: fully random
                candidateParams.RandomizeParameters();
            }
            else if (attempt < 30)
            {
                // Next set: focus on promising parameter ranges with some biasing
                candidateParams.RandomizeParameters();

                // Apply biasing to favor certain parameter ranges known to be promising
                foreach (var indicator in candidateParams)
                {
                    var parameters = indicator.GetParameters();
                    foreach (var param in parameters.GetProperties())
                    {
                        try
                        {
                            // Apply biasing based on parameter name heuristics
                            if (param.Name.Contains("Period") && param.Value is int periodValue)
                            {
                                // Favor medium periods for moving averages and similar indicators
                                int min = param.Min is int minVal ? minVal : 5;
                                int max = param.Max is int maxVal ? maxVal : 50;
                                int midPoint = (min + max) / 2;
                                int range = Math.Max(5, (max - min) / 4);

                                // Bias toward middle range and ensure it's within bounds
                                int newValue = Math.Min(max, Math.Max(min, random.Next(midPoint - range, midPoint + range + 1)));
                                param.Value = newValue;
                                parameters.UpdateFromDescriptor(param);
                            }
                            else if (param.Name.Contains("Threshold") && param.Value is double thresholdValue)
                            {
                                // Favor moderate thresholds
                                double min = param.Min is double minVal ? minVal : 0.0;
                                double max = param.Max is double maxVal ? maxVal : 1.0;
                                double midPoint = (min + max) / 2;
                                double range = (max - min) / 4;

                                // Bias toward middle range with some variation and ensure it's within bounds
                                double newValue = Math.Min(max, Math.Max(min, midPoint + (random.NextDouble() * 2 - 1) * range));
                                param.Value = newValue;
                                parameters.UpdateFromDescriptor(param);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Skip this parameter if there's an error
                            Console.WriteLine($"Error setting parameter {param.Name}: {ex.Message}");
                        }
                    }
                }
            }
            else if (attempt < 60)
            {
                // Later attempts: use latin hypercube sampling for better coverage
                candidateParams.RandomizeParameters();

                // Apply quasi-random distribution (simplified Latin Hypercube)
                int segmentCount = Math.Min(20, 60 - attempt);
                int segment = attempt % segmentCount;

                foreach (var indicator in candidateParams)
                {
                    var parameters = indicator.GetParameters();
                    foreach (var param in parameters.GetProperties())
                    {
                        try
                        {
                            if (param.Value is double dvalue && param.Min is double dmin && param.Max is double dmax)
                            {
                                // Divide parameter range into segments
                                double segmentSize = (dmax - dmin) / segmentCount;
                                // Choose a value within this segment with some noise
                                double baseValue = dmin + (segment * segmentSize);
                                // Ensure value is within bounds
                                double newValue = Math.Min(dmax, Math.Max(dmin, baseValue + random.NextDouble() * segmentSize));
                                param.Value = newValue;
                                parameters.UpdateFromDescriptor(param);
                            }
                            else if (param.Value is int ivalue && param.Min is int imin && param.Max is int imax)
                            {
                                // Same for integer parameters
                                int segmentSize = Math.Max(1, (imax - imin) / segmentCount);
                                int baseValue = imin + (segment * segmentSize);
                                // Ensure value is within bounds
                                int newValue = Math.Min(imax, Math.Max(imin, baseValue + random.Next(segmentSize)));
                                param.Value = newValue;
                                parameters.UpdateFromDescriptor(param);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Skip this parameter if there's an error
                            Console.WriteLine($"Error setting parameter {param.Name}: {ex.Message}");
                        }
                    }
                }
            }
            else
            {
                // Final attempts: combine global best with random elements
                candidateParams.RandomizeParameters();

                // This is a placeholder - in a full implementation, 
                // we would inject elements from the global best parameters
                // with some randomization to promote convergence while maintaining diversity
            }

            return candidateParams;
        }
    }
}