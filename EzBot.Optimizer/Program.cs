using EzBot.Common;
using EzBot.Core.Optimization;
using EzBot.Core.Strategy;
using EzBot.Models;
using System.Globalization;

// Configure command-line options
string? dataFilePath = "C:\\Users\\Ceezar89\\Desktop\\git\\ezbot\\Data\\btcusd_1-min_data.csv";
StrategyType strategyType = StrategyType.PrecisionTrend;
TimeFrame timeFrame = TimeFrame.OneHour;
double initialBalance = 1000;
double feePercentage = 0.05;
int lookbackDays = 1500;
int minTemperature = 5;
double defaultCoolingRate = 0.95;
int maxConcurrentTrades = 5;
double maxDrawdownPercent = 30;
int leverage = 10;
int daysInactiveLimit = 10;
double minWinRatePercent = 0.55;
int minTotalTrades = 10;
int threadCount = -1;

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
            case "--thread-count":
                if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedThreadCount))
                    threadCount = parsedThreadCount;
                break;
            case "--min-temperature":
                if (i + 1 < args.Length && double.TryParse(args[++i], NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedMinTemperature))
                    minTemperature = (int)parsedMinTemperature;
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
                if (i + 1 < args.Length && double.TryParse(args[++i], NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedMaxDrawdownPercent))
                    maxDrawdownPercent = parsedMaxDrawdownPercent;
                break;
            case "--leverage":
                if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedLeverage))
                    leverage = parsedLeverage;
                break;
            case "--min-total-trades":
                if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedMinTotalTrades))
                    minTotalTrades = parsedMinTotalTrades;
                break;
            case "--strategy":
                if (i + 1 < args.Length && Enum.TryParse(args[++i], true, out StrategyType parsed))
                    strategyType = parsed;
                break;
            case "--days-inactive-limit":
                if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedDaysInactiveLimit))
                    daysInactiveLimit = parsedDaysInactiveLimit;
                break;
            case "--min-win-rate":
                if (i + 1 < args.Length && double.TryParse(args[++i], NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedMinWinRatePercent))
                    minWinRatePercent = parsedMinWinRatePercent;
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
            case "--info":
                PrintUsage();
                return;
        }
    }
}

// If no output file is specified, generate one based on strategy type, timeframe, and lookback
if (string.IsNullOrEmpty(outputFile))
{
    outputFile = strategyType.ToString() + "_" + timeFrame.ToString() + "_" + lookbackDays.ToString() + "d.json";
}

// threads can be -1 (use all -1 threads), 0 (use all available threads), or a positive integer
threadCount = threadCount == -1 ? Environment.ProcessorCount - 1 : threadCount == 0 ? Environment.ProcessorCount : threadCount;
if (threadCount > Environment.ProcessorCount) threadCount = Environment.ProcessorCount;

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
    var historicalData = CsvDataUtility.LoadBarDataFromCsv(dataFilePath);

    // Start the optimization process
    string timeframeDisplay = TimeFrameUtility.GetTimeFrameDisplayName(timeFrame);
    Console.WriteLine($"\nOptimizing {strategyType} strategy on {timeframeDisplay} timeframe.");
    Console.WriteLine($"Initial balance: ${initialBalance}, Fee: {feePercentage}%");

    Console.WriteLine();

    var optimizer = new StrategyOptimizer(
        historicalData,
        strategyType,
        timeFrame,
        initialBalance,
        feePercentage,
        lookbackDays,
        threadCount,
        minTemperature,
        defaultCoolingRate,
        maxConcurrentTrades,
        maxDrawdownPercent,
        leverage,
        daysInactiveLimit,
        minWinRatePercent,
        minTotalTrades,
        outputFile
    );

    var result = optimizer.FindOptimalParameters();

    // Show backtest results
    var best = result.BacktestResult;

    // Clear the progress bar line and move to next line
    Console.WriteLine();

    Console.WriteLine($"\n=== OPTIMIZATION COMPLETE ===");

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
        Console.WriteLine($"  Max Drawdown: {best.MaxDrawdown:F2}%");
        Console.WriteLine($"  Max Days Inactive: {best.MaxDaysInactive} days");
        Console.WriteLine($"  Sharpe Ratio: {best.SharpeRatio:F2}");
        Console.WriteLine($"  Start Date: {best.StartDate}");
        Console.WriteLine($"  End Date: {best.EndDate}");
        Console.WriteLine($"  Backtest Trading Days: {best.BacktestDurationDays} days");

        // Save final results
        Console.WriteLine("\nChecking if final results should be saved...");
        optimizer.SaveFinalResult(result);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}

static void PrintUsage()
{
    Console.WriteLine("EzBot Strategy Optimizer");
    Console.WriteLine("Usage: EzBot.Optimizer [options]");
    Console.WriteLine("\nOptions:");
    Console.WriteLine("--file                       CSV file with historical price data");
    Console.WriteLine("--strategy                   Strategy type (default: PrecisionTrend)");
    Console.WriteLine("--timeframe                  Timeframe for backtesting (e.g., 1m, 15m, 1h, 4h, 1d)");
    Console.WriteLine("--balance                    Initial balance for backtest (default: 1000)");
    Console.WriteLine("--fee                        Trading fee percentage (default: 0.05)");
    Console.WriteLine("--output                     Output JSON file (default: optimization_result.json)");
    Console.WriteLine("--help                       Show this help message");
    Console.WriteLine("--lookback-days              Number of days to look back (default: 1500)");
    Console.WriteLine("--thread-count               Number of threads to use (default: 1)");
    Console.WriteLine("--min-temperature            Minimum temperature for the optimization (default: 5)");
    Console.WriteLine("--cooling-rate               Cooling rate for the optimization (default: 0.95)");
    Console.WriteLine("--max-concurrent-trades      Maximum number of concurrent trades (default: 5)");
    Console.WriteLine("--max-drawdown               Maximum drawdown percentage (default: 30)");
    Console.WriteLine("--leverage                   Leverage (default: 10)");
    Console.WriteLine("--days-inactive-limit        Days inactive limit (default: 10)");
    Console.WriteLine("--min-win-rate               Minimum win rate percentage (default: 0.55)");
    Console.WriteLine("--min-total-trades           Minimum total trades (default: 10)");
    Console.WriteLine("\nExample:");
    Console.WriteLine("  EzBot.Optimizer --file data/BTCUSDT_1m.csv --timeframe 15m --min-temperature 100 --cooling-rate 0.85 --max-concurrent-trades 5 --max-drawdown 30 --leverage 10 --days-inactive-limit 30");
}
