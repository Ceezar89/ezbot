using EzBot.Common;
using EzBot.Core.Factory;
using EzBot.Core.Optimization;
using EzBot.Core.Strategy;
using EzBot.Models;
using System.Globalization;
using System.Text.Json;

// Configure command-line options
// string? dataFilePath = "C:\\Users\\Ceezar89\\Desktop\\git\\ezbot\\Data\\btc_data.example.csv";
string? dataFilePath = "C:\\Users\\Ceezar89\\Desktop\\git\\ezbot\\Data\\btcusd_1-min_data.csv";
StrategyType strategyType = StrategyType.PrecisionTrend;
TimeFrame timeFrame = TimeFrame.Hour1;
double initialBalance = 1000;
double feePercentage = 0.025;
string outputFile = "optimization_result.json";
int iterations = 1000;

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
            case "-i":
            case "--iterations":
                if (i + 1 < args.Length && int.TryParse(args[++i], out int iters))
                    iterations = iters;
                break;
            case "-s":
            case "--strategy":
                if (i + 1 < args.Length && Enum.TryParse(args[++i], true, out StrategyType parsed))
                    strategyType = parsed;
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
    Console.WriteLine($"Loading historical data from {dataFilePath}...");
    var historicalData = CsvDataUtility.LoadBarDataFromCsv(dataFilePath);
    // keep the last 50_000 bars
    // historicalData = [.. historicalData.Skip(historicalData.Count - 50_000)];
    Console.WriteLine($"Loaded {historicalData.Count} bars of historical data.");

    // Start the optimization process
    string timeframeDisplay = TimeFrameUtility.GetTimeFrameDisplayName(timeFrame);
    Console.WriteLine($"\nOptimizing {strategyType} strategy on {timeframeDisplay} timeframe...");
    Console.WriteLine($"Initial balance: ${initialBalance}, Fee: {feePercentage}%");

    // Create a progress callback
    static void progressCallback(int current, int total)
    {
        DisplayProgressBar(current, total);
    }

    while (true)
    {

        // Run optimization with progress tracking
        Console.WriteLine("\nProgress:");

        var result = new OptimizerRunner(
            historicalData,
            strategyType,
            timeFrame,
            iterations,
            progressCallback)
        .FindOptimalParameters();

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
            // Display results
            Console.WriteLine($"Tested {result.TotalCombinationsTested} parameter combinations on {timeframeDisplay} timeframe");

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
            Console.WriteLine($"  Win Rate: {best.WinRate:F2}% ({best.WinningTrades}/{best.TotalTrades})");
            Console.WriteLine($"  Total Trades: {best.TotalTrades}");
            Console.WriteLine($"  Profit Factor: {best.ProfitFactor:F2}");
            Console.WriteLine($"  Max Drawdown: {best.MaxDrawdown:F2}%");
            Console.WriteLine($"  Sharpe Ratio: {best.SharpeRatio:F2}");
            Console.WriteLine($"  Start Date: {best.StartDate}");
            Console.WriteLine($"  End Date: {best.EndDate}");
            Console.WriteLine($"  Backtest Duration: {best.BacktestDurationDays} days");

            // Save to file
            // Create a static JsonSerializerOptions to be reused
            var jsonOptions = GetJsonSerializerOptions();

            // Helper method that returns cached JsonSerializerOptions
            static JsonSerializerOptions GetJsonSerializerOptions()
            {
                return new JsonSerializerOptions { WriteIndented = true };
            }
            string json = JsonSerializer.Serialize(result, jsonOptions);
            File.WriteAllText(outputFile, json);
            Console.WriteLine($"\nResults saved to {outputFile}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}

static void DisplayProgressBar(int current, int total)
{
    const int barWidth = 50;

    // Calculate percentage and bar segments
    double percent = (double)current / total;
    int filledWidth = (int)Math.Floor(barWidth * percent);

    // Build the progress bar string
    string bar = "[" + new string('■', filledWidth) + new string(' ', barWidth - filledWidth) + "]";

    // Display progress
    Console.Write($"\r{bar} {current}/{total} ({percent:P2})");
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
    Console.WriteLine("  --fee PERCENTAGE      Trading fee percentage (default: 0.025)");
    Console.WriteLine("  -o, --output FILE     Output JSON file (default: optimization_result.json)");
    Console.WriteLine("  -h, --help            Show this help message");
    Console.WriteLine("  -i, --iterations      Number of iterations to run (default: 1000)");
    Console.WriteLine("\nExample:");
    Console.WriteLine("  EzBot.Optimizer -f data/BTCUSDT_1m.csv -t 15m -m 100 -b 10000");
}
