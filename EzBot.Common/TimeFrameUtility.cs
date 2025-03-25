using EzBot.Models;

namespace EzBot.Common;

public static class TimeFrameUtility
{
    public static List<BarData> ConvertTimeFrame(List<BarData> sourceData, TimeFrame targetTimeFrame)
    {
        // If target is 1 minute or sourceData is empty, just return the source data
        if (targetTimeFrame == TimeFrame.Minute1 || sourceData.Count == 0)
        {
            return sourceData;
        }

        var result = new List<BarData>();
        int minutes = (int)targetTimeFrame;

        // Group bars by their timeframe buckets
        var groupedBars = sourceData
            .GroupBy(bar => bar.TimeStamp / (60 * minutes))
            .OrderBy(g => g.Key);

        foreach (var group in groupedBars)
        {
            var bars = group.OrderBy(b => b.TimeStamp).ToList();
            if (bars.Count == 0) continue;

            // Create a new aggregated bar
            var newBar = new BarData
            {
                // Use the timestamp of the first bar in the group
                TimeStamp = bars[0].TimeStamp,
                // Open price is the open of the first bar
                Open = bars[0].Open,
                // High price is the highest high in the group
                High = bars.Max(b => b.High),
                // Low price is the lowest low in the group
                Low = bars.Min(b => b.Low),
                // Close price is the close of the last bar
                Close = bars[^1].Close,
                // Volume is the sum of all volumes
                Volume = bars.Sum(b => b.Volume)
            };

            result.Add(newBar);
        }

        return result;
    }

    public static string GetTimeFrameDisplayName(TimeFrame timeFrame)
    {
        return timeFrame switch
        {
            TimeFrame.Minute1 => "1m",
            TimeFrame.Minute5 => "5m",
            TimeFrame.Minute15 => "15m",
            TimeFrame.Minute30 => "30m",
            TimeFrame.OneHour => "1h",
            TimeFrame.TwoHour => "2h",
            TimeFrame.FourHour => "4h",
            TimeFrame.EightHour => "8h",
            TimeFrame.TwelveHour => "12h",
            TimeFrame.OneDay => "1d",
            TimeFrame.OneWeek => "1w",
            _ => $"{(int)timeFrame}m"
        };
    }

    public static TimeFrame ParseTimeFrame(string input)
    {
        // Remove any whitespace
        input = input.Trim().ToLower();

        // Check for time specifiers
        if (input.EndsWith("m"))
        {
            if (int.TryParse(input[0..^1], out int minutes))
            {
                return minutes switch
                {
                    1 => TimeFrame.Minute1,
                    5 => TimeFrame.Minute5,
                    15 => TimeFrame.Minute15,
                    30 => TimeFrame.Minute30,
                    _ => (TimeFrame)minutes // Custom minute value
                };
            }
        }
        else if (input.EndsWith("h"))
        {
            if (int.TryParse(input[0..^1], out int hours))
            {
                return hours switch
                {
                    1 => TimeFrame.OneHour,
                    2 => TimeFrame.TwoHour,
                    4 => TimeFrame.FourHour,
                    8 => TimeFrame.EightHour,
                    12 => TimeFrame.TwelveHour,
                    _ => (TimeFrame)(hours * 60) // Convert hours to minutes
                };
            }
        }
        else if (input.EndsWith("d"))
        {
            if (int.TryParse(input[0..^1], out int days))
            {
                return days == 1 ? TimeFrame.OneDay : (TimeFrame)(days * 1440);
            }
        }
        else if (input.EndsWith("w"))
        {
            if (int.TryParse(input[0..^1], out int weeks))
            {
                return weeks == 1 ? TimeFrame.OneWeek : (TimeFrame)(weeks * 10080);
            }
        }

        // Default to 1 minute if parsing fails
        return TimeFrame.Minute1;
    }
}