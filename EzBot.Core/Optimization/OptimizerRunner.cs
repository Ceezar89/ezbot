using EzBot.Core.Extensions;
using EzBot.Core.Factory;
using EzBot.Core.Indicator;
using EzBot.Core.Strategy;
using EzBot.Models;
using EzBot.Common;
using System.Collections.Concurrent;

namespace EzBot.Core.Optimization
{
    public class OptimizerRunner(
        List<BarData> historicalData,
        StrategyType strategyType,
        TimeFrame timeFrame = TimeFrame.Hour1,
        int iterations = 1000,
        Action<int, int>? progressCallback = null,
        double initialBalance = 1000,
        double feePercentage = 0.025,
        int maxConcurrentTrades = 5,
        double maxDrawdownPercent = 30,
        int leverage = 10
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
        private readonly int iterations = iterations;

        public OptimizationResult FindOptimalParameters()
        {
            // Convert the data to the desired timeframe
            var timeframeData = TimeFrameUtility.ConvertTimeFrame(historicalData, timeFrame);
            var parameters = new IndicatorCollection(strategyType);

            // Run multiple optimization instances in parallel
            int instanceCount = Math.Max(1, Environment.ProcessorCount);
            var allResults = new ConcurrentBag<(IndicatorCollection Params, BacktestResult Result)>();
            int iterationsPerInstance = iterations / instanceCount;

            // Create a progress tracker for all instances
            var completedIterations = 0;
            object progressLock = new object();

            // Create a progress callback for each instance
            Action<int, int> instanceProgressCallback = (current, total) =>
            {
                if (progressCallback == null) return;

                lock (progressLock)
                {
                    // Calculate progress as a portion of total iterations across all instances
                    // Each instance contributes (current/total) * iterationsPerInstance iterations
                    int oldCompleted = completedIterations;

                    // Calculate new completed iterations across all instances
                    // The +1 is to avoid zero progress at the start
                    int instanceContribution = (int)Math.Floor((double)(current * iterationsPerInstance) / total) + 1;
                    completedIterations = instanceContribution;

                    // Only update the display if progress has changed
                    if (oldCompleted != completedIterations)
                    {
                        // Report progress to the main progress callback
                        progressCallback(Math.Min(completedIterations, iterations), iterations);
                    }
                }
            };

            // Run parallel optimization instances
            Parallel.For(0, instanceCount, instanceIndex =>
            {
                // Clone parameters for this instance
                var instanceParams = parameters.DeepClone();
                // Use different random seed for each instance
                instanceParams.RandomizeParameters(new Random(Guid.NewGuid().GetHashCode()));

                // Run optimization with this instance's iterations
                var instanceResults = RunSingleOptimization(
                    timeframeData,
                    instanceParams,
                    iterationsPerInstance,
                    instanceProgressCallback);

                // Collect all results
                foreach (var result in instanceResults)
                {
                    allResults.Add(result);
                }
            });

            // Report 100% completion when done
            progressCallback?.Invoke(iterations, iterations);

            // Further process and return the results
            var resultsWithParams = allResults.ToList();

            // Rest of your existing code for processing results...
            if (resultsWithParams.Count == 0)
            {
                return new OptimizationResult
                {
                    BestParameters = new IndicatorCollection(strategyType).ToDto(),
                    BacktestResult = new BacktestResult(),
                    AllResults = [],
                    TotalCombinationsTested = 0,
                    TimeFrame = timeFrame
                };
            }

            // Remove trades that don't have any trades
            resultsWithParams = [.. resultsWithParams.Where(r => r.Result.TotalTrades > 0)];

            // Remove trades that have a max drawdown higher than the limit
            resultsWithParams = [.. resultsWithParams.Where(r => r.Result.MaxDrawdownPercent <= maxDrawdownPercent)];

            // Find the best result, truncate the results to the top 100
            var (bestParameters, bestResult) = resultsWithParams
                .OrderByDescending(pair => pair.Result.NetProfit)
                .First();

            // Collect all results without their parameters
            var allBacktestResults = resultsWithParams.Select(pair => pair.Result).ToList();

            // truncate the results to the top 100
            allBacktestResults = [.. allBacktestResults.Take(100)];

            return new OptimizationResult
            {
                BestParameters = bestParameters.ToDto(),
                BacktestResult = bestResult,
                AllResults = allBacktestResults,
                TotalCombinationsTested = allBacktestResults.Count,
                TimeFrame = timeFrame
            };
        }

        public BacktestResult RunBacktest(ITradingStrategy strategy, List<BarData> historicalData)
        {
            var account = new BacktestAccount(initialBalance, feePercentage, leverage);
            Dictionary<int, TradeOrder> activeOrders = [];

            // Skip some initial bars to allow indicators to initialize
            int warmupPeriod = 100;
            if (historicalData.Count <= warmupPeriod)
                throw new ArgumentException("Not enough data for backtesting");

            // Track last trade activity
            int lastTradeBarIndex = warmupPeriod; // Initialize to the first bar after warmup

            // Calculate how many bars represent one hour based on timeframe
            double barsPerHour = 60.0 / (int)timeFrame;

            int inactivityThreshold = (int)(240 * barsPerHour); // no trade in 10 days

            account.StartUnixTime = historicalData[0].TimeStamp + warmupPeriod * (int)timeFrame;

            for (int i = warmupPeriod; i < historicalData.Count; i++)
            {
                account.EndUnixTime = historicalData[i].TimeStamp;

                if (account.IsLiquidated)
                    return account.GenerateResult();

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

                // Check for excessive inactivity
                if (i - lastTradeBarIndex > inactivityThreshold && i > warmupPeriod + inactivityThreshold)
                {
                    account.LiquidateAccount();
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

        private List<(string SerializedParams, BacktestResult Result)> GeneticAlgorithmOptimization(
            List<BarData> timeframeData,
            IndicatorCollection parameters)
        {
            throw new NotImplementedException("Genetic Algorithm optimization is not implemented yet");
        }

        private List<(IndicatorCollection Params, BacktestResult Result)> ParticleSwarmOptimization(
            List<BarData> timeframeData,
            IndicatorCollection parameters)
        {
            // Store results
            var results = new ConcurrentBag<(IndicatorCollection Params, BacktestResult Result)>();
            var random = new Random();

            // PSO parameters
            int swarmSize = 30;
            int numIterations = 300;
            double inertiaWeight = 0.7;
            double cognitiveWeight = 1.5;
            double socialWeight = 1.5;

            // Initialize particles
            var particles = new List<Particle>();
            IndicatorCollection globalBestPosition = [];
            double globalBestFitness = double.MinValue;
            BacktestResult globalBestResult = new();

            // Initialize swarm
            for (int i = 0; i < swarmSize; i++)
            {
                parameters.RandomizeParameters(random);

                var strategy = StrategyFactory.CreateStrategy(strategyType, parameters);
                BacktestResult result = RunBacktest(strategy, timeframeData);

                double fitness = CalculateEnergy(result);

                var particle = new Particle
                {
                    Position = parameters,
                    Velocity = GenerateRandomVelocity(parameters),
                    PersonalBestPosition = parameters,
                    PersonalBestFitness = fitness,
                    CurrentFitness = fitness,
                    Result = result
                };

                particles.Add(particle);
                results.Add((parameters, result));

                // Update global best if needed
                if (fitness > globalBestFitness)
                {
                    globalBestFitness = fitness;
                    globalBestPosition = parameters;
                    globalBestResult = result;
                }
            }

            // Main PSO loop
            for (int iteration = 0; iteration < numIterations; iteration++)
            {
                // Report progress
                progressCallback?.Invoke(iteration, numIterations);

                // Process each particle in parallel
                Parallel.ForEach(particles, particle =>
                {
                    // Update velocity and position
                    var currentParams = particle.Position;
                    var personalBestParams = particle.PersonalBestPosition;
                    var globalBestParams = globalBestPosition;

                    // Apply PSO formula to update parameters
                    foreach (var indicator in currentParams)
                    {
                        var currentIndicatorParams = indicator.GetParameters();
                        var personalBestIndicatorParams = personalBestParams.First(i => i.GetType() == indicator.GetType()).GetParameters();
                        var globalBestIndicatorParams = globalBestParams.First(i => i.GetType() == indicator.GetType()).GetParameters();

                        foreach (var param in currentIndicatorParams.GetProperties())
                        {
                            var paramName = param.Name;
                            var personalBestParam = personalBestIndicatorParams.GetProperties().First(p => p.Name == paramName);
                            var globalBestParam = globalBestIndicatorParams.GetProperties().First(p => p.Name == paramName);

                            if (param.Value is double dvalue && param.Min is double dmin && param.Max is double dmax)
                            {
                                double personalBestValue = (double)personalBestParam.Value;
                                double globalBestValue = (double)globalBestParam.Value;

                                // Calculate inertia, cognitive, and social components
                                double inertia = inertiaWeight * particle.Velocity[paramName];
                                double cognitive = cognitiveWeight * random.NextDouble() * (personalBestValue - dvalue);
                                double social = socialWeight * random.NextDouble() * (globalBestValue - dvalue);

                                // Update velocity and position
                                particle.Velocity[paramName] = inertia + cognitive + social;
                                double newValue = dvalue + particle.Velocity[paramName];

                                // Ensure the new value is within bounds
                                newValue = Math.Max(dmin, Math.Min(dmax, newValue));

                                // Update the parameter value
                                param.Value = newValue;
                                currentIndicatorParams.UpdateFromDescriptor(param);
                            }
                            else if (param.Value is int ivalue && param.Min is int imin && param.Max is int imax)
                            {
                                int personalBestValue = (int)personalBestParam.Value;
                                int globalBestValue = (int)globalBestParam.Value;

                                // Calculate components for integer parameters
                                double inertia = inertiaWeight * particle.Velocity[paramName];
                                double cognitive = cognitiveWeight * random.NextDouble() * (personalBestValue - ivalue);
                                double social = socialWeight * random.NextDouble() * (globalBestValue - ivalue);

                                // Update velocity and position
                                particle.Velocity[paramName] = inertia + cognitive + social;
                                int newValue = ivalue + (int)Math.Round(particle.Velocity[paramName]);

                                // Ensure the new value is within bounds
                                newValue = Math.Max(imin, Math.Min(imax, newValue));

                                // Update the parameter value
                                param.Value = newValue;
                                currentIndicatorParams.UpdateFromDescriptor(param);
                            }
                            else if (param.Value is bool bvalue)
                            {
                                bool personalBestValue = (bool)personalBestParam.Value;
                                bool globalBestValue = (bool)globalBestParam.Value;

                                // For boolean parameters, use probability-based approach
                                double probability = random.NextDouble();
                                if (probability < 0.1) // 10% chance to flip
                                {
                                    param.Value = !bvalue;
                                    currentIndicatorParams.UpdateFromDescriptor(param);
                                }
                            }
                        }
                    }

                    // Update particle position
                    particle.Position = currentParams;

                    // Evaluate new position
                    var strategy = StrategyFactory.CreateStrategy(strategyType, currentParams);
                    BacktestResult result = RunBacktest(strategy, timeframeData);
                    double fitness = CalculateEnergy(result);
                    particle.CurrentFitness = fitness;
                    particle.Result = result;

                    // Add to results
                    results.Add((particle.Position, result));

                    // Update personal best if needed
                    if (fitness > particle.PersonalBestFitness)
                    {
                        particle.PersonalBestPosition = particle.Position;
                        particle.PersonalBestFitness = fitness;
                    }
                });

                // Update global best
                foreach (var particle in particles)
                {
                    if (particle.PersonalBestFitness > globalBestFitness)
                    {
                        globalBestFitness = particle.PersonalBestFitness;
                        globalBestPosition = particle.PersonalBestPosition;
                        globalBestResult = particle.Result;
                    }
                }
            }

            // Ensure the best solution is in the results list
            if (!results.Any(r => r.Params == globalBestPosition))
            {
                results.Add((globalBestPosition, globalBestResult));
            }

            return [.. results];
        }

        // Helper class for PSO
        private class Particle
        {
            public required IndicatorCollection Position { get; set; }
            public Dictionary<string, double> Velocity { get; set; } = [];
            public required IndicatorCollection PersonalBestPosition { get; set; }
            public double PersonalBestFitness { get; set; }
            public double CurrentFitness { get; set; }
            public required BacktestResult Result { get; set; }
        }

        // Helper method to generate random velocity vectors
        private static Dictionary<string, double> GenerateRandomVelocity(IndicatorCollection parameters)
        {
            var random = new Random();
            var velocity = new Dictionary<string, double>();

            foreach (var indicator in parameters)
            {
                var indicatorParams = indicator.GetParameters();
                foreach (var param in indicatorParams.GetProperties())
                {
                    if (param.Value is double && param.Min is double dmin && param.Max is double dmax)
                    {
                        // Initialize with small random velocity
                        velocity[param.Name] = (random.NextDouble() * 2 - 1) * (dmax - dmin) * 0.1;
                    }
                    else if (param.Value is int && param.Min is int imin && param.Max is int imax)
                    {
                        // Initialize with small random velocity for integers
                        velocity[param.Name] = (random.NextDouble() * 2 - 1) * (imax - imin) * 0.1;
                    }
                    else if (param.Value is bool)
                    {
                        // For boolean parameters, use a small value
                        velocity[param.Name] = random.NextDouble() * 0.2;
                    }
                }
            }

            return velocity;
        }

        private List<(IndicatorCollection Params, BacktestResult Result)> SimulatedAnnealingOptimization(
            List<BarData> timeframeData,
            IndicatorCollection parameters)
        {
            // Store results
            var results = new List<(IndicatorCollection Params, BacktestResult Result)>();

            // Simulated annealing parameters
            double initialTemperature = 100.0;
            double finalTemperature = 0.1;
            double coolingRate = Math.Pow(finalTemperature / initialTemperature, 1.0 / iterations);

            // Random number generator
            var random = new Random();

            // Initialize with random parameters
            var currentParams = parameters.DeepClone();
            currentParams.RandomizeParameters(random);

            // Evaluate initial solution
            var strategy = StrategyFactory.CreateStrategy(strategyType, currentParams);
            var currentResult = RunBacktest(strategy, timeframeData);
            double currentEnergy = -CalculateEnergy(currentResult);

            // Track best solution
            var bestParams = currentParams.DeepClone();
            var bestResult = currentResult;
            double bestEnergy = currentEnergy;

            // Add initial solution to results
            if (currentResult.IsValidResult)
            {
                results.Add((currentParams.DeepClone(), currentResult));
            }

            // Main simulated annealing loop
            double temperature = initialTemperature;
            for (int i = 0; i < iterations; i++)
            {
                // Report progress
                progressCallback?.Invoke(i, iterations);

                // Create neighbor solution
                var neighborParams = currentParams.DeepClone();
                PerturbParameters(neighborParams, temperature / initialTemperature);

                // Evaluate neighbor solution
                strategy = StrategyFactory.CreateStrategy(strategyType, neighborParams);
                BacktestResult neighborResult;
                try
                {
                    neighborResult = RunBacktest(strategy, timeframeData);
                    if (!neighborResult.IsValidResult)
                    {
                        // Skip invalid solutions
                        temperature *= coolingRate;
                        continue;
                    }
                }
                catch (Exception)
                {
                    // Skip failed evaluations
                    temperature *= coolingRate;
                    continue;
                }

                // Calculate energies
                double neighborEnergy = -CalculateEnergy(neighborResult);

                // Decide whether to accept the neighbor solution
                bool accept = false;
                if (neighborEnergy <= currentEnergy)
                {
                    // Always accept better solutions
                    accept = true;
                }
                else
                {
                    // Accept worse solutions with decreasing probability
                    double acceptanceProbability = Math.Exp((currentEnergy - neighborEnergy) / temperature);
                    accept = random.NextDouble() < acceptanceProbability;
                }

                if (accept)
                {
                    currentParams = neighborParams;
                    currentResult = neighborResult;
                    currentEnergy = neighborEnergy;

                    // Record this result
                    results.Add((currentParams.DeepClone(), currentResult));

                    // Update best solution if needed
                    if (currentEnergy < bestEnergy)
                    {
                        bestEnergy = currentEnergy;
                        bestParams = currentParams.DeepClone();
                        bestResult = currentResult;
                    }
                }

                // Cool down temperature
                temperature *= coolingRate;
            }

            // Ensure the best solution is in the results
            if (!results.Any(r => r.Params.Equals(bestParams)))
            {
                results.Add((bestParams, bestResult));
            }

            return [.. results.Where(r => r.Result.IsValidResult)];
        }

        // Calculate the "energy" of a solution - lower is better in simulated annealing
        private double CalculateEnergy(BacktestResult result)
        {
            // Composite score using multiple metrics, with higher values being better
            double profitFactorWeight = 3.0;
            double netProfitWeight = 1.0;
            double winRateWeight = 2.0;
            double sharpeWeight = 2.0;
            double drawdownPenalty = 1.0;

            double score = 0;

            // Add profit factor (avoid division by zero)
            if (!double.IsNaN(result.ProfitFactor) && !double.IsInfinity(result.ProfitFactor))
            {
                score += result.ProfitFactor * profitFactorWeight;
            }

            // Add normalized net profit
            score += (result.NetProfit / initialBalance) * netProfitWeight;

            // Add win rate
            if (result.TotalTrades > 0)
            {
                double winRate = (double)result.WinningTrades / result.TotalTrades;
                score += winRate * winRateWeight;
            }

            // Add Sharpe ratio if available
            if (!double.IsNaN(result.SharpeRatio) && !double.IsInfinity(result.SharpeRatio))
            {
                score += Math.Min(result.SharpeRatio, 3.0) * sharpeWeight; // Cap at 3.0
            }

            // Penalize high drawdowns
            double drawdownFactor = result.MaxDrawdownPercent / 100.0;
            score -= Math.Pow(drawdownFactor, 2) * drawdownPenalty;

            return -score; // Negative because we want to maximize the score
        }

        // Perturb parameters randomly based on the current temperature ratio
        private static void PerturbParameters(IndicatorCollection parameters, double temperatureRatio)
        {
            var random = new Random();

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

        // Helper method to run a single optimization instance
        private List<(IndicatorCollection Params, BacktestResult Result)> RunSingleOptimization(
            List<BarData> timeframeData,
            IndicatorCollection parameters,
            int iterationCount,
            Action<int, int>? instanceProgressCallback)
        {
            // Store results
            var results = new List<(IndicatorCollection Params, BacktestResult Result)>();

            // Simulated annealing parameters
            double initialTemperature = 100.0;
            double finalTemperature = 0.1;
            double coolingRate = Math.Pow(finalTemperature / initialTemperature, 1.0 / iterationCount);

            // Random number generator
            var random = new Random();

            // Initialize with random parameters
            var currentParams = parameters.DeepClone();

            // Evaluate initial solution
            var strategy = StrategyFactory.CreateStrategy(strategyType, currentParams);
            var currentResult = RunBacktest(strategy, timeframeData);
            double currentEnergy = -CalculateEnergy(currentResult);

            // Track best solution
            var bestParams = currentParams.DeepClone();
            var bestResult = currentResult;
            double bestEnergy = currentEnergy;

            // Add initial solution to results
            if (currentResult.IsValidResult)
            {
                results.Add((currentParams.DeepClone(), currentResult));
            }

            // Main simulated annealing loop
            double temperature = initialTemperature;
            for (int i = 0; i < iterationCount; i++)
            {
                // Report progress
                instanceProgressCallback?.Invoke(i, iterationCount);

                // The rest of the simulated annealing algorithm
                // [Same implementation as in the simplified SimulatedAnnealingOptimization method]

                // Create neighbor solution
                var neighborParams = currentParams.DeepClone();
                PerturbParameters(neighborParams, temperature / initialTemperature);

                // Evaluate neighbor solution
                strategy = StrategyFactory.CreateStrategy(strategyType, neighborParams);
                BacktestResult neighborResult;
                try
                {
                    neighborResult = RunBacktest(strategy, timeframeData);
                    if (!neighborResult.IsValidResult)
                    {
                        // Skip invalid solutions
                        temperature *= coolingRate;
                        continue;
                    }
                }
                catch (Exception)
                {
                    // Skip failed evaluations
                    temperature *= coolingRate;
                    continue;
                }

                // Calculate energies
                double neighborEnergy = -CalculateEnergy(neighborResult);

                // Decide whether to accept the neighbor solution
                bool accept = false;
                if (neighborEnergy <= currentEnergy)
                {
                    // Always accept better solutions
                    accept = true;
                }
                else
                {
                    // Accept worse solutions with decreasing probability
                    double acceptanceProbability = Math.Exp((currentEnergy - neighborEnergy) / temperature);
                    accept = random.NextDouble() < acceptanceProbability;
                }

                if (accept)
                {
                    currentParams = neighborParams;
                    currentResult = neighborResult;
                    currentEnergy = neighborEnergy;

                    // Record this result
                    results.Add((currentParams.DeepClone(), currentResult));

                    // Update best solution if needed
                    if (currentEnergy < bestEnergy)
                    {
                        bestEnergy = currentEnergy;
                        bestParams = currentParams.DeepClone();
                        bestResult = currentResult;
                    }
                }

                // Cool down temperature
                temperature *= coolingRate;
            }

            // Ensure the best solution is in the results
            if (!results.Any(r => r.Params.Equals(bestParams)))
            {
                results.Add((bestParams, bestResult));
            }

            return results.Where(r => r.Result.IsValidResult).ToList();
        }
    }
}