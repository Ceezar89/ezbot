using EzBot.Common;
using EzBot.Core.Optimization;
using EzBot.Core.Strategy;
using EzBot.Models;
using System.Globalization;

// Configure command-line options
string? dataFilePath = "../data/btcusd_data.csv";
StrategyType strategyType = StrategyType.PrecisionTrend;
TimeFrame timeFrame = TimeFrame.OneHour;
double initialBalance = 1000;
double feePercentage = 0.05;
int lookbackDays = 1500;
double minTemperature = 0.05;
double defaultCoolingRate = 0.95;
int maxConcurrentTrades = 5;
double maxDrawdown = 0.5;
int leverage = 10;
double minWinRate = 0.5;
int threadCount = -1;
int daysInactiveLimit = 30;
bool usePreviousResult = false;
string outputFile = "";

// Parse command line arguments
if (args.Length > 0)
{
    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i].ToLower())
        {
            case "--file":
                if (i + 1 < args.Length) dataFilePath = args[++i];
                break;
            case "--use-prev-result":
                usePreviousResult = true;
                break;
            case "--days-inactive-limit":
                if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedDaysInactiveLimit))
                    daysInactiveLimit = parsedDaysInactiveLimit;
                break;
            case "--thread-count":
                if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedThreadCount))
                    threadCount = parsedThreadCount;
                break;
            case "--min-temperature":
                if (i + 1 < args.Length && double.TryParse(args[++i], NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedMinTemperature))
                    minTemperature = parsedMinTemperature;
                break;
            case "--cooling-rate":
                if (i + 1 < args.Length && double.TryParse(args[++i], NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedDefaultCoolingRate))
                    defaultCoolingRate = parsedDefaultCoolingRate;
                break;
            case "--max-concurrent-trades":
                if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedMaxConcurrentTrades))
                    maxConcurrentTrades = parsedMaxConcurrentTrades;
                break;
            case "--max-drawdown":
                if (i + 1 < args.Length && double.TryParse(args[++i], NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedMaxDrawdown))
                    maxDrawdown = parsedMaxDrawdown;
                break;
            case "--leverage":
                if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedLeverage))
                    leverage = parsedLeverage;
                break;
            case "--strategy":
                if (i + 1 < args.Length && Enum.TryParse(args[++i], true, out StrategyType parsed))
                    strategyType = parsed;
                break;
            case "--min-win-rate":
                if (i + 1 < args.Length && double.TryParse(args[++i], NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedMinWinRate))
                    minWinRate = parsedMinWinRate;
                break;
            case "--lookback-days":
                if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedLookbackDays))
                    lookbackDays = parsedLookbackDays;
                break;
            case "--timeframe":
                if (i + 1 < args.Length)
                    timeFrame = TimeFrameUtility.ParseTimeFrame(args[++i]);
                break;
            case "--balance":
                if (i + 1 < args.Length && double.TryParse(args[++i], NumberStyles.Any, CultureInfo.InvariantCulture, out double balance))
                    initialBalance = balance;
                break;
            case "--fee":
                if (i + 1 < args.Length && double.TryParse(args[++i], NumberStyles.Any, CultureInfo.InvariantCulture, out double fee))
                    feePercentage = fee;
                break;
            case "--output":
                if (i + 1 < args.Length) outputFile = args[++i];
                break;
        }
    }
}

// If no output file is specified, generate one based on strategy type, timeframe, and lookback
if (string.IsNullOrEmpty(outputFile))
{
    outputFile = strategyType.ToString() + "_" + timeFrame.ToString() + "_" + lookbackDays.ToString() + "d.json";
}

// If no data file is specified, ask for one or use default
if (string.IsNullOrEmpty(dataFilePath))
{
    Console.Write("Enter path to historical data CSV file: ");
    dataFilePath = Console.ReadLine();

    if (string.IsNullOrEmpty(dataFilePath))
    {
        Console.WriteLine("No data file provided. Exiting.");
        return;
    }
}

try
{
    var optimizer = new StrategyOptimizer(
        dataFilePath,
        strategyType,
        timeFrame,
        initialBalance,
        feePercentage,
        lookbackDays,
        threadCount,
        minTemperature,
        defaultCoolingRate,
        maxConcurrentTrades,
        maxDrawdown,
        leverage,
        daysInactiveLimit,
        minWinRate,
        outputFile,
        usePreviousResult
    );

    // print all the parameters
    Console.WriteLine($"Data File: {dataFilePath}");
    Console.WriteLine($"Strategy Type: {strategyType}");
    Console.WriteLine($"Timeframe: {timeFrame}");
    Console.WriteLine($"Initial Balance: {initialBalance}");
    Console.WriteLine($"Fee Percentage: {feePercentage}");
    Console.WriteLine($"Lookback Days: {lookbackDays}");
    Console.WriteLine($"Min Temperature: {minTemperature}");
    Console.WriteLine($"Cooling Rate: {defaultCoolingRate}");
    Console.WriteLine($"Max Concurrent Trades: {maxConcurrentTrades}");
    Console.WriteLine($"Max Drawdown: {maxDrawdown * 100:F0}%");
    Console.WriteLine($"Leverage: {leverage}x");
    Console.WriteLine($"Days Inactive Limit: {daysInactiveLimit} days");
    Console.WriteLine($"Min Win Rate: {minWinRate * 100:F0}%");
    Console.WriteLine($"Output File: {outputFile}");

    while (true)
    {
        var result = optimizer.FindOptimalParameters();

        // Show backtest results
        var best = result.BacktestResult;

        if (best.TotalTrades == 0)
        {
            Console.WriteLine("No valid trades found. Please try increasing the number of iterations or changing the strategy.");
        }
        else
        {
            // Show best parameters
            Console.WriteLine("\nBest Parameters:");
            foreach (var param in result.BestParameters)
            {
                Console.WriteLine($"  {param.IndicatorType}:");
                foreach (var (key, value) in param.Parameters)
                {
                    Console.WriteLine($"    {key}: {value}");
                }
            }

            Console.WriteLine("\nBacktest Results:");
            Console.WriteLine($"  Net Profit: ${best.NetProfit:F2} ({best.ReturnPercentage:F2}%)");
            Console.WriteLine($"  Win Rate: {best.WinRatePercent:F2}% ({best.WinningTrades}/{best.TotalTrades})");
            Console.WriteLine($"  Total Trades: {best.TotalTrades}");
            var terminatedEarly = best.TerminatedEarly ? "Yes" : "No";
            Console.WriteLine($"  Terminated Early: {terminatedEarly}");
            Console.WriteLine($"  Max Drawdown: {best.MaxDrawdown * 100:F2}%");
            Console.WriteLine($"  Max Days Inactive: {best.MaxDaysInactive} days");
            Console.WriteLine($"  Sharpe Ratio: {best.SharpeRatio:F2}");
            Console.WriteLine($"  Start Date: {best.StartDate}");
            Console.WriteLine($"  End Date: {best.EndDate}");
            Console.WriteLine($"  Backtest Trading Duration: {best.BacktestDurationDays} days");

            // Save final results
            Console.WriteLine("\nChecking if final results should be saved...");
            optimizer.SaveFinalResult(result);
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}

