using EzBot.Models;
using System.Globalization;

namespace EzBot.Common;

public static class CsvDataUtility
{
    public static List<BarData> LoadBarDataFromCsv(string filePath, bool hasHeader = true)
    {
        List<BarData> bars = [];

        using var reader = new StreamReader(filePath);

        // Skip header if present
        if (hasHeader)
        {
            reader.ReadLine();
        }

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var values = line.Split(',');
            if (values.Length >= 6)
            {
                // Handle timestamp with decimal part (e.g., "1325412060.0")
                string timestampStr = values[0].Split('.')[0]; // Get only the integer part

                var bar = new BarData
                {
                    TimeStamp = long.Parse(timestampStr, CultureInfo.InvariantCulture),
                    Open = double.Parse(values[1], CultureInfo.InvariantCulture),
                    High = double.Parse(values[2], CultureInfo.InvariantCulture),
                    Low = double.Parse(values[3], CultureInfo.InvariantCulture),
                    Close = double.Parse(values[4], CultureInfo.InvariantCulture),
                    Volume = double.Parse(values[5], CultureInfo.InvariantCulture)
                };
                bars.Add(bar);
            }
        }
        return bars;
    }

    public static void SaveBarDataToCsv(List<BarData> bars, string filePath, bool includeHeader = true)
    {
        using var writer = new StreamWriter(filePath);

        if (includeHeader)
        {
            writer.WriteLine("Timestamp,Open,High,Low,Close,Volume");
        }

        foreach (var bar in bars)
        {
            writer.WriteLine($"{bar.TimeStamp},{bar.Open.ToString(CultureInfo.InvariantCulture)}," +
                             $"{bar.High.ToString(CultureInfo.InvariantCulture)}," +
                             $"{bar.Low.ToString(CultureInfo.InvariantCulture)}," +
                             $"{bar.Close.ToString(CultureInfo.InvariantCulture)}," +
                             $"{bar.Volume.ToString(CultureInfo.InvariantCulture)}");
        }
    }
}