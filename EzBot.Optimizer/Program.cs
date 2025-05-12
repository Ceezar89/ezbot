using EzBot.Core.Optimization;
using EzBot.Core.Strategy;
using EzBot.Models;
using System.Globalization;

string dataFilePath = "../data/btcusd_data.csv";

List<StrategyConfiguration> strategyConfigurations = [];

// Add your strategy configurations here
strategyConfigurations.Add(new StrategyConfiguration([
    IndicatorType.Etma,
    IndicatorType.NormalizedVolume,
    IndicatorType.Lwpi,
    IndicatorType.Trendilo
]));

strategyConfigurations.Add(new StrategyConfiguration([
    IndicatorType.McGinley,
    IndicatorType.NormalizedVolume,
    IndicatorType.Lwpi,
    IndicatorType.Trendilo
]));

strategyConfigurations.Add(new StrategyConfiguration([
    IndicatorType.Etma,
    IndicatorType.NormalizedVolume,
    IndicatorType.Trendilo
]));

strategyConfigurations.Add(new StrategyConfiguration([
    IndicatorType.McGinley,
    IndicatorType.NormalizedVolume,
    IndicatorType.Trendilo
]));

strategyConfigurations.Add(new StrategyConfiguration([
    IndicatorType.Etma,
    IndicatorType.NormalizedVolume,
    IndicatorType.Lwpi
]));

strategyConfigurations.Add(new StrategyConfiguration([
    IndicatorType.McGinley,
    IndicatorType.NormalizedVolume,
    IndicatorType.Lwpi
]));

List<TimeFrame> timeFrames = [TimeFrame.OneHour];
List<double> riskPercentages = [1.0, 2.0, 3.0];
List<int> maxConcurrentTrades = [1, 2, 3, 4, 5];

double initialBalance = 1000;
double feePercentage = 0.05;
double maxDrawdown = 0.3;
int lookbackDays = 1500;
int leverage = 10;
int threadCount = -1;
bool runSavedConfiguration = false;
double maxInactivityPercentage = 0.05;

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
            case "--max-concurrent-trades":
                if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedMaxConcurrentTrades))
                    maxConcurrentTrades = [parsedMaxConcurrentTrades];
                break;
            case "--max-drawdown":
                if (i + 1 < args.Length && double.TryParse(args[++i], NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedMaxDrawdown))
                    maxDrawdown = parsedMaxDrawdown;
                break;
            case "--leverage":
                if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedLeverage))
                    leverage = parsedLeverage;
                break;
            case "--lookback-days":
                if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedLookbackDays))
                    lookbackDays = parsedLookbackDays;
                break;
            case "--balance":
                if (i + 1 < args.Length && double.TryParse(args[++i], NumberStyles.Any, CultureInfo.InvariantCulture, out double balance))
                    initialBalance = balance;
                break;
            case "--fee":
                if (i + 1 < args.Length && double.TryParse(args[++i], NumberStyles.Any, CultureInfo.InvariantCulture, out double fee))
                    feePercentage = fee;
                break;
            case "--run-saved":
                runSavedConfiguration = true;
                break;
            case "--max-inactivity-percent":
                if (i + 1 < args.Length && double.TryParse(args[++i], NumberStyles.Any, CultureInfo.InvariantCulture, out double percentage))
                    maxInactivityPercentage = percentage / 100;
                break;
        }
    }
}

int totalProgressCount = strategyConfigurations.Count * riskPercentages.Count * timeFrames.Count * maxConcurrentTrades.Count;
int currentProgress = 0;

try
{
    foreach (var maxTrades in maxConcurrentTrades)
    {
        foreach (var riskPercentage in riskPercentages)
        {
            foreach (var timeFrame in timeFrames)
            {
                foreach (var strategyConfiguration in strategyConfigurations)
                {
                    // Print total progress count
                    Console.WriteLine($"================================================");
                    Console.WriteLine($"Current progress: {++currentProgress}/{totalProgressCount}");
                    Console.WriteLine($"================================================");

                    var tester = new StrategyTester(
                        dataFilePath: dataFilePath,
                        strategyConfiguration: strategyConfiguration,
                        timeFrame: timeFrame,
                        initialBalance: initialBalance,
                        feePercentage: feePercentage,
                        maxConcurrentTrades: maxTrades,
                        leverage: leverage,
                        lookbackDays: lookbackDays,
                        threadCount: threadCount,
                        maxDrawdown: maxDrawdown,
                        riskPercentage: riskPercentage,
                        maxInactivityPercentage: maxInactivityPercentage,
                        runSavedConfiguration: runSavedConfiguration
                    );
                    tester.Test();
                }
            }
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}