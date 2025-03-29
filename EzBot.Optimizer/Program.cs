using EzBot.Common;
using EzBot.Core.Optimization;
using EzBot.Core.Strategy;
using EzBot.Models;
using System.Globalization;
using System.Text.Json;

// Configure command-line options
string? dataFilePath = "C:\\Users\\Ceezar89\\Desktop\\git\\ezbot\\Data\\btcusd_1-min_data.csv";
StrategyType strategyType = StrategyType.PrecisionTrend;
TimeFrame timeFrame = TimeFrame.OneHour;
double initialBalance = 1000;
double feePercentage = 0.05;
int lookback = 1_000_000;
int minTemperature = 10;
double defaultCoolingRate = 0.85;
int maxConcurrentTrades = 5;
double maxDrawdownPercent = 30;
int leverage = 10;
int daysInactiveLimit = 30;
double minWinRatePercent = 0.55;
int minTotalTrades = 30;

string outputFile = "";

// Parse command line arguments
if (args.Length > 0)
{
    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i].ToLower())
        {
            case "-f":
            case "--file":
                if (i + 1 < args.Length) dataFilePath = args[++i];
                break;
            case "-m":
            case "--min-temperature":
                if (i + 1 < args.Length && double.TryParse(args[++i], NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedMinTemperature))
                    minTemperature = (int)parsedMinTemperature;
                break;
            case "-c":
            case "--cooling-rate":
                if (i + 1 < args.Length && double.TryParse(args[++i], NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedDefaultCoolingRate))
                    defaultCoolingRate = parsedDefaultCoolingRate;
                break;
            case "-x":
            case "--max-concurrent-trades":
                if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedMaxConcurrentTrades))
                    maxConcurrentTrades = parsedMaxConcurrentTrades;
                break;
            case "-d":
            case "--max-drawdown":
                if (i + 1 < args.Length && double.TryParse(args[++i], NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedMaxDrawdownPercent))
                    maxDrawdownPercent = parsedMaxDrawdownPercent;
                break;
            case "-l":
            case "--leverage":
                if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedLeverage))
                    leverage = parsedLeverage;
                break;
            case "-i":
            case "--min-total-trades":
                if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedMinTotalTrades))
                    minTotalTrades = parsedMinTotalTrades;
                break;
            case "-s":
            case "--strategy":
                if (i + 1 < args.Length && Enum.TryParse(args[++i], true, out StrategyType parsed))
                    strategyType = parsed;
                break;
            case "-y":
            case "--days-inactive-limit":
                if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedDaysInactiveLimit))
                    daysInactiveLimit = parsedDaysInactiveLimit;
                break;
            case "-w":
            case "--min-win-rate":
                if (i + 1 < args.Length && double.TryParse(args[++i], NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedMinWinRatePercent))
                    minWinRatePercent = parsedMinWinRatePercent;
                break;
            case "-k":
            case "--lookback":
                if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedLookback))
                    lookback = parsedLookback;
                break;
            case "-t":
            case "--timeframe":
                if (i + 1 < args.Length)
                    timeFrame = TimeFrameUtility.ParseTimeFrame(args[++i]);
                break;
            case "-b":
            case "--balance":
                if (i + 1 < args.Length && double.TryParse(args[++i], NumberStyles.Any, CultureInfo.InvariantCulture, out double balance))
                    initialBalance = balance;
                break;
            case "-e":
            case "--fee":
                if (i + 1 < args.Length && double.TryParse(args[++i], NumberStyles.Any, CultureInfo.InvariantCulture, out double fee))
                    feePercentage = fee;
                break;
            case "-o":
            case "--output":
                if (i + 1 < args.Length) outputFile = args[++i];
                break;
            case "-h":
            case "--help":
                PrintUsage();
                return;
        }
    }
}

// If no output file is specified, generate one based on strategy type, timeframe, and lookback
if (string.IsNullOrEmpty(outputFile))
{
    outputFile = strategyType.ToString() + "_" + timeFrame.ToString() + "_" + (lookback / 60 / 24).ToString() + "d.json";
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
    // Load historical data
    Console.WriteLine("");
    Console.WriteLine($"Loading historical data from {dataFilePath}...");
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
        lookback,
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
    Console.WriteLine("  -f, --file FILE       CSV file with historical price data");
    Console.WriteLine("  -s, --strategy TYPE   Strategy type (default: PrecisionTrend)");
    Console.WriteLine("  -t, --timeframe FRAME Timeframe for backtesting (e.g., 1m, 15m, 1h, 4h, 1d)");
    Console.WriteLine("  -b, --balance AMOUNT  Initial balance for backtest (default: 1000)");
    Console.WriteLine("  -e, --fee PERCENTAGE  Trading fee percentage (default: 0.025)");
    Console.WriteLine("  -o, --output FILE     Output JSON file (default: optimization_result.json)");
    Console.WriteLine("  -h, --help            Show this help message");
    Console.WriteLine("  -i, --iterations      Number of iterations to run (default: 1000)");
    Console.WriteLine("  -m, --min-temperature Minimum temperature for the optimization (default: 10)");
    Console.WriteLine("  -c, --cooling-rate    Cooling rate for the optimization (default: 0.85)");
    Console.WriteLine("  -x, --max-concurrent-trades Maximum number of concurrent trades (default: 5)");
    Console.WriteLine("  -d, --max-drawdown    Maximum drawdown percentage (default: 30)");
    Console.WriteLine("  -l, --leverage        Leverage (default: 10)");
    Console.WriteLine("  -y, --days-inactive-limit Days inactive limit (default: 30)");
    Console.WriteLine("\nExample:");
    Console.WriteLine("  EzBot.Optimizer -f data/BTCUSDT_1m.csv -t 15m -m 100 -b 10000");
}
