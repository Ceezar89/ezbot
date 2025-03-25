using EzBot.Core.Extensions;
using EzBot.Core.Factory;
using EzBot.Core.Indicator;
using EzBot.Core.Strategy;
using EzBot.Models;
using EzBot.Common;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace EzBot.Core.Optimization
{
    // Simple atomic counter for thread-safe operations
    internal class AtomicCounter
    {
        private int _value;

        public AtomicCounter(int initialValue)
        {
            _value = initialValue;
        }

        public int Value => Interlocked.CompareExchange(ref _value, 0, 0);

        public int Increment()
        {
            return Interlocked.Increment(ref _value);
        }

        public int Decrement()
        {
            return Interlocked.Decrement(ref _value);
        }
    }

    public class StrategyOptimizer(
        List<BarData> historicalData,
        StrategyType strategyType,
        TimeFrame timeFrame = TimeFrame.OneHour,
        Action<int, int>? progressCallback = null,
        double initialBalance = 1000,
        double feePercentage = 0.04,
        int maxConcurrentTrades = 5,
        double maxDrawdownPercent = 30,
        int leverage = 10,
        int daysInactiveLimit = 30
        )
    {
        private readonly List<BarData> historicalData = historicalData;
        private readonly StrategyType strategyType = strategyType;
        private readonly TimeFrame timeFrame = timeFrame;
        private readonly Action<int, int>? progressCallback = progressCallback;
        private readonly double initialBalance = initialBalance;
        private readonly double feePercentage = feePercentage;
        private readonly int maxConcurrentTrades = maxConcurrentTrades;
        private readonly double maxDrawdownPercent = maxDrawdownPercent;
        private readonly int leverage = leverage;
        private readonly int daysInactiveLimit = daysInactiveLimit;

        public OptimizationResult FindOptimalParameters()
        {
            // Convert the data to the desired timeframe
            var timeframeData = TimeFrameUtility.ConvertTimeFrame(historicalData, timeFrame);

            // Determine the number of worker threads
            int workerCount = Environment.ProcessorCount;

            // Create a concurrent bag to collect all results
            var allResults = new ConcurrentBag<(IndicatorCollection Params, BacktestResult Result)>();

            // Track progress and completion
            int totalEvaluatedCombinations = 0;
            object progressLock = new();

            try
            {
                // --- Phase 1: Use all threads to find one good starting candidate ---
                Console.WriteLine($"Phase 1: Randomizing parameters using {workerCount} threads to find a good starting candidate");

                // Shared state for Phase 1
                using var candidateFound = new ManualResetEventSlim(false);
                var phase1BestCandidate = new Tuple<IndicatorCollection?, BacktestResult?>(null, null);
                var phase1Lock = new object();

                // Add a timeout for Phase 1 (5 minutes)
                using var cancellationTokenSource = new CancellationTokenSource();
                // var timeoutTask = Task.Run(() =>
                // {
                //     try
                //     {
                //         if (!cancellationTokenSource.Token.WaitHandle.WaitOne(TimeSpan.FromMinutes(20)))
                //         {
                //             Console.WriteLine("Phase 1 timeout reached, proceeding with best candidate so far");
                //             candidateFound.Set();
                //         }
                //     }
                //     catch (OperationCanceledException)
                //     {
                //         // Normal when the token is canceled
                //     }
                // }, cancellationTokenSource.Token);

                // Use CountdownEvent to track active threads in Phase 1
                using var activeThreadsPhase1 = new CountdownEvent(workerCount);

                // Use a ManualResetEventSlim to signal when Phase 2 can start
                using var phase2ReadyToStart = new ManualResetEventSlim(false);

                // Create and start the tasks for phase 1 with dynamic allocation
                var phase1Tasks = new List<Task>(workerCount);
                for (int i = 0; i < workerCount; i++)
                {
                    phase1Tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            FindGoodCandidateCollaboratively(
                                timeframeData,
                                candidateFound,
                                phase1BestCandidate,
                                phase1Lock,
                                () =>
                                {
                                    lock (progressLock)
                                    {
                                        totalEvaluatedCombinations++;
                                        progressCallback?.Invoke(totalEvaluatedCombinations, 0);
                                    }
                                });
                        }
                        finally
                        {
                            // Decrement the active thread count
                            activeThreadsPhase1.Signal();

                            // If this was the last Phase 1 thread to finish or we found a candidate
                            if (activeThreadsPhase1.CurrentCount == 0 || candidateFound.IsSet)
                            {
                                // Cancel the timeout if all threads finished before timeout
                                cancellationTokenSource.Cancel();
                                phase2ReadyToStart.Set();
                            }
                        }
                    }));
                }

                // Wait for Phase 1 to complete or for a candidate to be found
                phase2ReadyToStart.Wait();

                // Extract the best candidate found in phase 1
                IndicatorCollection? bestCandidate;
                BacktestResult? bestCandidateResult;

                lock (phase1Lock)
                {
                    bestCandidate = phase1BestCandidate.Item1;
                    bestCandidateResult = phase1BestCandidate.Item2;
                }

                // Ensure we have a viable candidate
                if (bestCandidate == null)
                {
                    Console.WriteLine("Warning: No viable candidate found in phase 1, creating a random one");
                    bestCandidate = new IndicatorCollection(strategyType);
                    bestCandidate.RandomizeParameters();
                    var strategy = StrategyFactory.CreateStrategy(strategyType, bestCandidate);
                    bestCandidateResult = RunBacktest(strategy, timeframeData);
                }

                // Add the best candidate to results
                if (bestCandidateResult != null && bestCandidateResult.TotalTrades > 0 && bestCandidate != null)
                {
                    allResults.Add((bestCandidate.DeepClone(), bestCandidateResult));
                }

                // --- Phase 2: Use all threads to optimize the single candidate ---
                Console.WriteLine($"Phase 2: Using {workerCount} threads to optimize single candidate with win rate: {bestCandidateResult?.WinRate:P2}");

                // Partition the temperature range to allow threads to explore different areas
                double[] startingTemperatures = new double[workerCount];
                for (int i = 0; i < workerCount; i++)
                {
                    // Create slightly different temperatures to encourage diverse exploration
                    startingTemperatures[i] = 100.0 * (0.9 + 0.2 * i / workerCount);
                }

                // Use a semaphore to limit concurrent execution based on CPU availability
                using var throttle = new SemaphoreSlim(workerCount);

                // Create a concurrent queue of work items for better load balancing
                var workItems = new ConcurrentQueue<(int threadIndex, double temperature)>();

                // Add initial work items with varied temperature settings
                for (int i = 0; i < workerCount; i++)
                {
                    workItems.Enqueue((i, startingTemperatures[i]));

                    // Create additional temperature variations to ensure work queue doesn't empty too quickly
                    workItems.Enqueue((i, startingTemperatures[i] * 0.8));
                    workItems.Enqueue((i, startingTemperatures[i] * 1.2));
                }

                // Shared state for communicating good results between threads
                var phase2BestResult = new ConcurrentBag<(IndicatorCollection Parameters, BacktestResult Result, double Fitness)>();

                // Process work items from the queue
                using var phase2CompletionEvent = new CountdownEvent(workerCount);
                var phase2Active = new AtomicCounter(workerCount);

                var phase2Tasks = new List<Task>(workerCount);
                for (int i = 0; i < workerCount; i++)
                {
                    phase2Tasks.Add(Task.Run(async () =>
                    {
                        while (workItems.TryDequeue(out var work))
                        {
                            await throttle.WaitAsync();
                            try
                            {
                                var results = ParallelSimulatedAnnealing(
                                    timeframeData,
                                    bestCandidate!.DeepClone(),
                                    work.temperature,
                                    work.threadIndex,
                                    (_, __) =>
                                    {
                                        lock (progressLock)
                                        {
                                            totalEvaluatedCombinations++;
                                            progressCallback?.Invoke(totalEvaluatedCombinations, 0);
                                        }
                                    });

                                // Process and share results with other threads
                                foreach (var result in results)
                                {
                                    // Only store results with good fitness to avoid memory bloat
                                    if (result.Result.TotalTrades > 0 && result.Result.MaxDrawdownPercent <= maxDrawdownPercent)
                                    {
                                        double fitness = CalculateFitness(result.Result);
                                        phase2BestResult.Add((result.Params, result.Result, fitness));

                                        // Add only the top results to the final collection
                                        allResults.Add(result);
                                    }
                                }
                            }
                            finally
                            {
                                throttle.Release();
                            }
                        }

                        // Decrement active thread count and signal completion
                        if (phase2Active.Decrement() == 0)
                        {
                            // Ensure all remaining threads can complete by adding a signal for each
                            for (int j = 0; j < workerCount; j++)
                            {
                                phase2CompletionEvent.Signal();
                            }
                        }
                        else
                        {
                            phase2CompletionEvent.Signal();
                        }
                    }));
                }

                // Adaptive task creation - periodically check if we need more work items
                using var adaptiveCts = new CancellationTokenSource();
                var adaptiveWorkTask = Task.Run(async () =>
                {
                    try
                    {
                        while (phase2Active.Value > 0 && !adaptiveCts.Token.IsCancellationRequested)
                        {
                            await Task.Delay(5000, adaptiveCts.Token); // Check every 5 seconds

                            // If queue is getting empty but we still have active threads, add more work
                            if (workItems.Count < workerCount / 2 && phase2Active.Value > 1)
                            {
                                // Get best results found so far
                                var bestResults = phase2BestResult
                                    .OrderByDescending(x => x.Fitness)
                                    .Take(Math.Min(3, phase2BestResult.Count))
                                    .ToList();

                                // If we have good results, add more work based on them
                                if (bestResults.Count > 0)
                                {
                                    foreach (var bestItem in bestResults)
                                    {
                                        // Add variations of the best parameters with different temperatures
                                        for (int i = 0; i < workerCount / 2; i++)
                                        {
                                            workItems.Enqueue((i, 50.0 * (0.8 + 0.4 * i / workerCount)));
                                        }
                                    }

                                    Console.WriteLine($"Added {workerCount / 2 * bestResults.Count} more work items based on promising results");
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Normal when canceled
                    }
                }, adaptiveCts.Token);

                // Wait for all Phase 2 tasks to complete
                phase2CompletionEvent.Wait();

                // Clean up adaptive task
                adaptiveCts.Cancel();
                try { adaptiveWorkTask.Wait(); } catch (OperationCanceledException) { } catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException)) { }

                // Final progress report after completion
                progressCallback?.Invoke(totalEvaluatedCombinations, totalEvaluatedCombinations);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Optimization exception: {ex.Message}");
                throw;
            }

            // Process the results
            var resultsWithParams = allResults.ToList();

            // Remove results that don't have any trades
            resultsWithParams = [.. resultsWithParams.Where(r => r.Result.TotalTrades > 0)];

            // Remove trades that have a max drawdown higher than the limit
            resultsWithParams = [.. resultsWithParams.Where(r => r.Result.MaxDrawdownPercent <= maxDrawdownPercent)];

            // Check if resultsWithParams is empty
            if (resultsWithParams.Count == 0)
            {
                return new OptimizationResult
                {
                    BestParameters = new IndicatorCollection(strategyType).ToDto(),
                    BacktestResult = new BacktestResult(),
                };
            }

            // Find the best result
            var (bestParameters, bestResult) = resultsWithParams
                .OrderByDescending(pair => pair.Result.NetProfit)
                .First();

            return new OptimizationResult
            {
                BestParameters = bestParameters.ToDto(),
                BacktestResult = bestResult,
            };
        }

        // Collaborative approach: all threads work together to find one good candidate
        private void FindGoodCandidateCollaboratively(
            List<BarData> timeframeData,
            ManualResetEventSlim candidateFound,
            Tuple<IndicatorCollection?, BacktestResult?> bestCandidate,
            object lockObject,
            Action progressCallback)
        {
            const double TargetWinRate = 0.5;

            // Keep track of the best candidate found by this thread
            IndicatorCollection threadBestCandidate = new(strategyType);
            BacktestResult threadBestResult = new();
            double threadBestFitness = double.MinValue;

            // Continue until either we find a good candidate or are interrupted
            while (!candidateFound.IsSet)
            {
                // Create a random set of parameters
                var candidateParams = new IndicatorCollection(strategyType);
                candidateParams.RandomizeParameters();

                // Evaluate the candidate
                var candidateStrategy = StrategyFactory.CreateStrategy(strategyType, candidateParams);
                var candidateResult = RunBacktest(candidateStrategy, timeframeData);

                // Signal progress
                progressCallback();

                // Check if this candidate meets our criteria
                if (candidateResult.TotalTrades > 0 &&
                    candidateResult.MaxDrawdownPercent <= maxDrawdownPercent &&
                    candidateResult.WinRate >= TargetWinRate)
                {
                    // Found a good candidate - update the shared result and signal other threads
                    lock (lockObject)
                    {
                        if (!candidateFound.IsSet)
                        {
                            bestCandidate = new Tuple<IndicatorCollection?, BacktestResult?>(candidateParams, candidateResult);
                            candidateFound.Set();
                            Console.WriteLine($"Found a good candidate with win rate: {candidateResult.WinRate:P2}");
                        }
                    }
                    return;
                }

                // Update this thread's best candidate if this is better
                if (candidateResult.TotalTrades > 0 && candidateResult.MaxDrawdownPercent <= maxDrawdownPercent)
                {
                    double fitness = CalculateFitness(candidateResult);
                    if (fitness > threadBestFitness)
                    {
                        threadBestCandidate = candidateParams;
                        threadBestResult = candidateResult;
                        threadBestFitness = fitness;
                    }
                }
            }

            // If we reach here, another thread found a good candidate
            // Check if our thread's best candidate is better than what was found
            lock (lockObject)
            {
                if (threadBestResult.TotalTrades > 0)
                {
                    // Check if this thread's best is better than the current shared best
                    if (bestCandidate.Item1 == null ||
                        bestCandidate.Item2 == null ||
                        CalculateFitness(threadBestResult) > CalculateFitness(bestCandidate.Item2))
                    {
                        bestCandidate = new Tuple<IndicatorCollection?, BacktestResult?>(threadBestCandidate, threadBestResult);
                        Console.WriteLine($"Updated best candidate with win rate: {threadBestResult.WinRate:P2}");
                    }
                }
            }
        }

        // Simulated annealing that allows different threads to explore from the same starting point
        private List<(IndicatorCollection Params, BacktestResult Result)> ParallelSimulatedAnnealing(
            List<BarData> timeframeData,
            IndicatorCollection initialParameters,
            double startingTemperature,
            int threadIndex,
            Action<int, int>? iterationCallback = null)
        {
            // Initial temperature and cooling rate for the simulated annealing process
            double InitialTemperature = startingTemperature;
            const double CoolingRate = 0.95;
            const double MinTemperature = 0.1;
            const int NoImprovementLimit = 100; // Stop after this many iterations without improvement

            var results = new List<(IndicatorCollection, BacktestResult)>();

            // Create a unique random seed for this thread
            var random = new Random(Guid.NewGuid().GetHashCode() + threadIndex);

            // Start with the provided parameters but add slight perturbation to ensure diversity
            var currentParams = initialParameters.DeepClone();

            // Add a small initial perturbation to differentiate between threads
            PerturbParameters(currentParams, 0.2, random);

            var currentStrategy = StrategyFactory.CreateStrategy(strategyType, currentParams);
            var currentResult = RunBacktest(currentStrategy, timeframeData);
            double currentEnergy = -CalculateFitness(currentResult);

            // Keep track of best solution found 
            var bestParameters = currentParams.DeepClone();
            var bestResult = currentResult;
            double bestEnergy = currentEnergy;

            // Signal that one combination was tested
            iterationCallback?.Invoke(0, 0);
            int completedIterations = 1;

            // Add initial solution to results if it's valid
            if (currentResult.TotalTrades > 0 && currentResult.MaxDrawdownPercent <= maxDrawdownPercent)
            {
                results.Add((currentParams.DeepClone(), currentResult));
            }

            // Start with the initial temperature
            double temperature = InitialTemperature;
            int acceptedSolutions = 0;
            int iterationsWithoutImprovement = 0;

            // Main simulated annealing loop with convergence detection
            while (temperature > MinTemperature && iterationsWithoutImprovement < NoImprovementLimit)
            {
                // Calculate temperature ratio for perturbation (1.0 at start, approaching 0.0 at end)
                double temperatureRatio = temperature / InitialTemperature;

                // Create a new candidate solution by perturbing the current parameters
                var candidateParams = currentParams.DeepClone();
                PerturbParameters(candidateParams, temperatureRatio, random);

                // Create and evaluate the candidate solution
                var candidateStrategy = StrategyFactory.CreateStrategy(strategyType, candidateParams);
                var candidateResult = RunBacktest(candidateStrategy, timeframeData);

                // Increment completed iterations and signal that one more combination was tested
                completedIterations++;
                iterationCallback?.Invoke(0, 0);

                // Skip invalid results early to save computation
                if (candidateResult.TotalTrades == 0 || candidateResult.MaxDrawdownPercent > maxDrawdownPercent)
                {
                    iterationsWithoutImprovement++;
                    continue;
                }

                double candidateEnergy = -CalculateFitness(candidateResult);

                // Calculate the acceptance probability
                double energyDelta = candidateEnergy - currentEnergy;
                double acceptanceProbability =
                    (energyDelta <= 0) ? 1.0 : Math.Exp(-energyDelta / temperature);

                // Decide whether to accept the new solution
                if (random.NextDouble() < acceptanceProbability)
                {
                    // Accept the candidate solution
                    currentParams = candidateParams;
                    currentResult = candidateResult;
                    currentEnergy = candidateEnergy;
                    acceptedSolutions++;

                    // Add accepted solution to results
                    results.Add((candidateParams.DeepClone(), candidateResult));

                    // Update the best solution if needed
                    if (candidateEnergy < bestEnergy)
                    {
                        bestParameters = candidateParams.DeepClone();
                        bestResult = candidateResult;
                        bestEnergy = candidateEnergy;
                        iterationsWithoutImprovement = 0; // Reset counter when we find an improvement
                    }
                    else
                    {
                        iterationsWithoutImprovement++;
                    }
                }
                else
                {
                    iterationsWithoutImprovement++;
                }

                // Cool down the temperature
                temperature *= CoolingRate;

                // Reheat if acceptance rate gets too low
                if (completedIterations % 100 == 0 && acceptedSolutions < 10)
                {
                    temperature = InitialTemperature * 0.5;
                    acceptedSolutions = 0;
                }
            }

            // Ensure the best solution is included in the results
            if (!results.Any(r => r.Item1.Equals(bestParameters) && r.Item2.Equals(bestResult)))
            {
                results.Add((bestParameters, bestResult));
            }

            return results;
        }

        private BacktestResult RunBacktest(ITradingStrategy strategy, List<BarData> historicalData)
        {
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

            account.StartUnixTime = historicalData[0].TimeStamp + warmupPeriod * (int)timeFrame;

            for (int i = warmupPeriod; i < historicalData.Count; i++)
            {
                account.EndUnixTime = historicalData[i].TimeStamp;

                var dataWindow = historicalData.GetRange(0, i + 1);

                // Handle existing positions
                var currentBar = historicalData[i];
                var activeTradeIds = activeOrders.Keys.ToList(); // Create a copy to safely iterate

                foreach (var tradeId in activeTradeIds)
                {
                    var order = activeOrders[tradeId];
                    var trade = account.GetTradeById(tradeId);

                    if (trade == null) continue;

                    if (order.TradeType == TradeType.Long)
                    {
                        if (currentBar.Low <= trade.StopLoss)
                        {
                            account.ClosePosition(tradeId, trade.StopLoss, i);
                            activeOrders.Remove(tradeId);

                        }
                        else if (currentBar.High >= order.TakeProfit)
                        {
                            account.ClosePosition(tradeId, order.TakeProfit, i);
                            activeOrders.Remove(tradeId);
                        }
                    }
                    else if (order.TradeType == TradeType.Short)
                    {
                        if (currentBar.High >= trade.StopLoss)
                        {
                            account.ClosePosition(tradeId, trade.StopLoss, i);
                            activeOrders.Remove(tradeId);
                        }
                        else if (currentBar.Low <= order.TakeProfit)
                        {
                            account.ClosePosition(tradeId, order.TakeProfit, i);
                            activeOrders.Remove(tradeId);
                        }
                    }
                }

                // Calculate days of inactivity
                int currentDaysInactive = (int)Math.Floor((i - lastTradeBarIndex) / barsPerDay);
                maxDaysInactive = Math.Max(maxDaysInactive, currentDaysInactive);

                // Check if we can open new positions
                if (activeOrders.Count < maxConcurrentTrades)
                {
                    var tradeOrder = strategy.GetAction(dataWindow);

                    if (tradeOrder.TradeType != TradeType.None)
                    {
                        int tradeId = account.OpenPosition(tradeOrder.TradeType,
                            historicalData[i].Close, tradeOrder.StopLoss, i);
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
                    return account.GenerateResult();
                }
            }

            // Close any remaining positions at the end
            foreach (var tradeId in activeOrders.Keys.ToList())
            {
                account.ClosePosition(tradeId, historicalData[^1].Close, historicalData.Count - 1);
            }

            return account.GenerateResult();
        }

        // Calculate fitness score for a backtest result
        private static double CalculateFitness(BacktestResult result)
        {
            // Avoid division by zero
            if (result.TotalTrades == 0)
                return 0;

            // Prioritize profitability but also consider other metrics
            double profitScore = result.NetProfit;
            double winRateScore = result.WinRate * 100;
            double drawdownPenalty = result.MaxDrawdownPercent * 2;
            double inactivityPenalty = result.MaxDaysInactive * 5;

            return profitScore + winRateScore - drawdownPenalty - inactivityPenalty;
        }

        // Perturb parameters randomly based on the current temperature ratio
        private static void PerturbParameters(IndicatorCollection parameters, double temperatureRatio, Random random)
        {
            // The higher the temperature, the more aggressive the perturbation
            foreach (var indicator in parameters)
            {
                var indicator_parameters = indicator.GetParameters();
                foreach (var param in indicator_parameters.GetProperties())
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
                        throw new InvalidOperationException("Unsupported parameter type");
                    }
                }
            }
        }
    }
}