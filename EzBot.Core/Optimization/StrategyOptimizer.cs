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
        int lookbackBars = 1_000_000,
        double minTemperature = 10.0,
        double defaultCoolingRate = 0.85,
        int maxConcurrentTrades = 5,
        double maxDrawdownPercent = 30,
        int leverage = 10,
        int daysInactiveLimit = 30,
        double minWinRatePercent = 0.55,
        int minTotalTrades = 30,
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

        // Changed to a concurrent queue for better performance with trimming
        private readonly ConcurrentQueue<(IndicatorCollection Params, BacktestResult Result, double Fitness)> results = [];

        // Track both tested hashes and a cache of backtests for similar parameter sets
        private readonly ConcurrentDictionary<int, byte> testedParameterHashes = new();
        private readonly ConcurrentDictionary<int, BacktestResult> backtestCache = new();
        private readonly int maxResultsToKeep = Math.Max(1000, Environment.ProcessorCount * 250);

        // Keep track of optimization metrics
        private readonly Stopwatch totalStopwatch = new(); // Total stopwatch for optimization duration
        private long totalBacktestsRun = 0; // Total number of backtests run
        private long cachedBacktestsUsed = 0; // Total number of cached backtests used
        private readonly Lock metricsLock = new(); // Lock for thread-safe metrics updates
        private const int DOUBLE_PRECISION = 4; // Precision for parameter discretization
        private double averageTemperature = 100.0; // Average temperature for progress reporting
        private DateTime lastSaveTime = DateTime.MinValue; // Track when we last saved results
        private const int SAVE_INTERVAL_SECONDS = 120; // Save every 2 minutes

        /// <summary>
        /// Find optimal parameters for the strategy using simulated annealing with work stealing.
        /// </summary>
        public OptimizationResult FindOptimalParameters()
        {
            totalStopwatch.Start();

            List<BarData> historicalData = [.. fullHistoricalData.Skip(fullHistoricalData.Count - lookbackBars)];
            Console.WriteLine($"Loaded {historicalData.Count:N0} bars of historical data.");

            // Convert the data to the desired timeframe
            var timeframeData = TimeFrameUtility.ConvertTimeFrame(historicalData, timeFrame);

            // Determine the number of worker threads
            int workerCount = Environment.ProcessorCount - 1;
            if (workerCount < 1) workerCount = 1;

            // Set minimum runtime to 2 minutes
            const int MinimumRuntimeMinutes = 2;
            TimeSpan minimumRuntime = TimeSpan.FromMinutes(MinimumRuntimeMinutes);

            // Step 1: Find good initial candidates in parallel
            var candidateTasks = new List<Task>();
            var cancellationTokenSource = new CancellationTokenSource();

            Console.WriteLine($"Starting {workerCount} randomization search tasks to find seed candidates.");

            // Launch worker tasks to find good initial candidates in parallel
            for (int i = 0; i < workerCount; i++)
            {
                candidateTasks.Add(Task.Run(() =>
                {
                    FindSeedCandidate(timeframeData, cancellationTokenSource.Token);
                }));
            }

            // Wait for at least one thread to find a good candidate, or until all tasks complete
            while (candidates.IsEmpty && !Task.WhenAll([.. candidateTasks]).IsCompleted)
            {
                Thread.Sleep(100); // Short sleep to avoid tight loop
            }

            // Cancel any remaining tasks once we have candidates
            if (!candidates.IsEmpty)
            {
                cancellationTokenSource.Cancel();
            }

            // Wait for all tasks to complete
            Task.WaitAll([.. candidateTasks]);

            // Step 2: Use the candidates as seeds for simulated annealing with work stealing

            // Find the best candidate to use as seed
            var (bestParams, bestResult) = candidates
                .OrderByDescending(c => CalculateFitness(c.Result))
                .First();

            Console.WriteLine($"Using best candidate with fitness: {CalculateFitness(bestResult):F2} and win rate: {bestResult.WinRatePercent:F2}% as seed.");
            Console.WriteLine($"Total unique parameter combinations tested: {testedParameterHashes.Count:N0}");

            // Create a shared work queue for all threads
            var sharedWorkQueue = new ConcurrentQueue<WorkItem>();

            // Maintain a best global result for exploration
            var globalBestResult = new AtomicReference<(IndicatorCollection Params, BacktestResult Result, double Fitness)>(
                (bestParams.DeepClone(), bestResult, CalculateFitness(bestResult)));

            // Initialize the queue with multiple starting points derived from the best candidate
            var parameterPerturbator = new ParameterPerturbator(DOUBLE_PRECISION);

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

            Console.WriteLine($"Starting dynamic work distribution with {workerCount} workers.");

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
                    for (int i = 0; i < workerCount * 10; i++)
                    {
                        var randomSeed = new Random(Guid.NewGuid().GetHashCode());
                        if (i % 2 == 0)
                        {
                            // Add random exploration
                            var freshParams = new IndicatorCollection(strategyType);
                            freshParams.RandomizeParameters();
                            sharedWorkQueue.Enqueue(new WorkItem
                            {
                                Parameters = freshParams,
                                Temperature = 100.0,
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
                                    Temperature = 60.0,
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

            // Add temperature information
            double tempProgress = 100.0 * (1.0 - ((averageTemperature - minTemperature) / (100.0 - minTemperature)));

            Console.WriteLine($"Current temperature: {averageTemperature:F2} (min: {minTemperature:F1})");

            Console.WriteLine("==================================================================");
            Console.WriteLine();

            // Periodically save the current best result
            SaveProgressIfNeeded(globalBest.Value);
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

                        if (previousResult != null && previousResult.BacktestResult.NetProfit > currentBest.Result.NetProfit)
                        {
                            Console.WriteLine($"Previous result in {outputFile} has better net profit (${previousResult.BacktestResult.NetProfit:F2} vs ${currentBest.Result.NetProfit:F2}).");
                            Console.WriteLine("Current result not saved.");
                            shouldSave = false;
                        }
                        else
                        {
                            Console.WriteLine($"Current result has better net profit than previous result. Saving...");
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
            List<BarData> timeframeData,
            CancellationToken cancellationToken
            )
        {
            var parameterPerturbator = new ParameterPerturbator(DOUBLE_PRECISION);
            int searchAttempts = 0;
            const int MaxSearchAttempts = 100;

            // Keep searching until we find a good candidate
            while (true)
            {
                searchAttempts++;
                // Check if cancellation was requested
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

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
                    Console.WriteLine($"Found a good candidate with win rate: {candidateResult.WinRatePercent:F2}%, drawdown: {candidateResult.MaxDrawdownPercent:F2}%");
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

                // Check if another thread has found a good candidate while we were searching
                if (!candidates.IsEmpty)
                {
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

                while (workQueue.TryDequeue(out var workItem))
                {
                    itemsProcessed++;

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
                            // Simple exponential moving average
                            averageTemperature = averageTemperature * 0.95 + temperature * 0.05;
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

                        // Check if this is the best result so far
                        var currentBest = globalBest.Value;

                        if (currentFitness > currentBest.Fitness)
                        {
                            globalBest.Value = resultTuple;
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
                        random.NextDouble() < 0.2)
                    {
                        // With some probability, create a fresh random starting point
                        var freshParams = new IndicatorCollection(strategyType);
                        freshParams.RandomizeParameters();

                        // Add a completely new work item
                        workQueue.Enqueue(new WorkItem
                        {
                            Parameters = freshParams,
                            Temperature = 100.0, // Reset temperature
                            PreviousBestWinRate = 0,
                            IterationsSinceImprovement = 0,
                            TotalIterations = 0,
                            AdaptiveCoolingRate = defaultCoolingRate,
                        });
                    }

                    if (shouldStop)
                    {
                        // Occasionally reinject a perturbed version of the global best
                        if (random.NextDouble() < 0.2)
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
                        workItem.Temperature *= coolingRate;
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
                        workItem.Temperature *= coolingRate;
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
                            Temperature = temperature * coolingRate,
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
                            Temperature = temperature * coolingRate,
                            PreviousBestWinRate = previousBestWinRate > 0 ? previousBestWinRate : currentWinRate,
                            IterationsSinceImprovement = iterationsSinceImprovement + 1,
                            TotalIterations = totalIterations + 1,
                            AdaptiveCoolingRate = coolingRate,
                        });
                    }

                    // Occasionally add diversity with different strategies
                    if (random.NextDouble() < 0.05)
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
                    else if (random.NextDouble() < 0.07)
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
                    else if (random.NextDouble() < 0.03)
                    {
                        // Strategy 3: Fresh random parameters for exploration
                        var freshParams = new IndicatorCollection(strategyType);
                        freshParams.RandomizeParameters();
                        workQueue.Enqueue(new WorkItem
                        {
                            Parameters = freshParams,
                            Temperature = 100.0, // High temperature for exploration
                            PreviousBestWinRate = 0,
                            IterationsSinceImprovement = 0,
                            TotalIterations = 0,
                            AdaptiveCoolingRate = defaultCoolingRate,
                        });
                    }
                }

                // Update global metrics before exit
                lock (metricsLock)
                {
                    totalBacktestsRun += workerBacktestsRun;
                    cachedBacktestsUsed += workerCachedHits;
                }

                // If the queue is empty but there are still active workers, inject new work
                if (workQueue.IsEmpty && activeWorkers > 1)
                {
                    // Add diversity by creating fresh random parameters
                    for (int i = 0; i < 5; i++)  // Add multiple items to increase chances of finding better solutions
                    {
                        var freshParams = new IndicatorCollection(strategyType);
                        freshParams.RandomizeParameters();
                        workQueue.Enqueue(new WorkItem
                        {
                            Parameters = freshParams,
                            Temperature = 100.0, // High temperature for exploration
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
                            ParameterPerturbator.PerturbParameters(diverseParams, 0.5, random); // Higher perturbation for diversity
                            workQueue.Enqueue(new WorkItem
                            {
                                Parameters = diverseParams,
                                Temperature = 50.0, // Medium temperature
                                PreviousBestWinRate = 0,
                                IterationsSinceImprovement = 0,
                                TotalIterations = 0,
                                AdaptiveCoolingRate = defaultCoolingRate,
                            });
                        }
                    }

                    Console.WriteLine($"Worker {workerId}: Queue was empty, injected 10 new exploration items");
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
            if (result.BacktestResult.TotalTrades > 0)
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

                        if (previousResult != null && previousResult.BacktestResult.NetProfit > result.BacktestResult.NetProfit)
                        {
                            Console.WriteLine($"\nPrevious result in {outputFile} has better net profit (${previousResult.BacktestResult.NetProfit:F2} vs ${result.BacktestResult.NetProfit:F2}).");
                            Console.WriteLine("Final result not saved.");
                            shouldSave = false;
                        }
                        else
                        {
                            Console.WriteLine($"\nCurrent result has better net profit than previous result.");
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

    // Extract parameter perturbation to a separate class for better organization
    public class ParameterPerturbator(int precision)
    {
        private readonly int _precision = precision;

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
                        double rounded = Math.Round(dvalue, _precision);
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