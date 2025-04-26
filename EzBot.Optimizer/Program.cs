using EzBot.Core.Optimization;
using EzBot.Core.Strategy;
using EzBot.Models;
using System.Globalization;

string dataFilePath = "../data/btcusd_data.csv";

List<StrategyType> strategyTypes = [StrategyType.EtmaTrend, StrategyType.McGinleyTrend];
List<TimeFrame> timeFrames = [TimeFrame.TwoHour, TimeFrame.OneHour, TimeFrame.ThirtyMinute, TimeFrame.FifteenMinute];
List<double> riskPercentages = [1.0, 2.0, 3.0];
List<int> maxConcurrentTrades = [1, 2, 3];

double initialBalance = 1000;
double feePercentage = 0.05;
double maxDrawdown = 0.3;
int lookbackDays = 1500;
int leverage = 10;
int threadCount = 0;
int maxDaysInactive = 3;

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
            case "--max-days-inactive":
                if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedMaxDaysInactive))
                    maxDaysInactive = parsedMaxDaysInactive;
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
                    strategyTypes = [parsed];
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
        }
    }
}

try
{
    foreach (var maxConcurrentTrade in maxConcurrentTrades)
    {
        foreach (var riskPercentage in riskPercentages)
        {
            foreach (var timeFrame in timeFrames)
            {
                foreach (var strategyType in strategyTypes)
                {
                    var tester = new StrategyTester(
                        dataFilePath,
                        strategyType,
                        timeFrame,
                        initialBalance,
                        feePercentage,
                        maxConcurrentTrade,
                        leverage,
                        lookbackDays,
                        threadCount,
                        maxDrawdown,
                        maxDaysInactive,
                        riskPercentage
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