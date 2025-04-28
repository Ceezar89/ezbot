using EzBot.Models;
using EzBot.Core.IndicatorParameter;

namespace EzBot.Core.Indicator;

public abstract class IndicatorBase<TParameter>(TParameter parameter) : IIndicator
    where TParameter : IIndicatorParameter
{
    protected TParameter Parameter { get; private set; } = parameter;
    private int? lastProcessedSignature;
    protected readonly Dictionary<long, int> ProcessedTimestamps = [];
    protected const int MaxStoredTimestamps = 100;

    public void SetBarData(IBarDataCollection bars)
    {
        if (bars is BarDataCollection collection)
        {
            if (lastProcessedSignature is null || lastProcessedSignature != collection.Signature)
            {
                ProcessBarData(collection.Bars);
                lastProcessedSignature = collection.Signature;
            }
        }
        else
        {
            // For other implementations, create a list as before
            var barsList = new List<BarData>(bars.Count);
            for (int i = 0; i < bars.Count; i++)
            {
                barsList.Add(bars[i]);
            }
            ProcessBarData(barsList);
        }
    }

    protected abstract void ProcessBarData(List<BarData> bars);

    public IIndicatorParameter GetParameters() => Parameter;

    public void UpdateParameters(IIndicatorParameter parameter)
    {
        Parameter = (TParameter)parameter;
        // Clear cached timestamps when parameters change
        ProcessedTimestamps.Clear();
    }

    protected int FindStartIndex(List<BarData> bars)
    {
        if (bars == null || bars.Count == 0)
            return 0;

        int startIndex = 0;

        // Find most recent timestamp we've already processed
        for (int i = 0; i < bars.Count; i++)
        {
            if (ProcessedTimestamps.TryGetValue(bars[i].TimeStamp, out int processedIndex))
            {
                if (i > 0 && processedIndex > 0)
                {
                    // We've already calculated this bar, so we can skip to the next
                    startIndex = i + 1;
                }
            }
            else
            {
                // If we find a bar we haven't processed yet, we'll start from here
                startIndex = i;
                break;
            }
        }

        return startIndex;
    }

    protected bool IsProcessed(long timestamp)
    {
        return ProcessedTimestamps.ContainsKey(timestamp);
    }

    protected void RecordProcessed(long timestamp, int index)
    {
        ProcessedTimestamps[timestamp] = index;

        // Limit dictionary size to avoid memory growth
        if (ProcessedTimestamps.Count > MaxStoredTimestamps)
        {
            // Remove oldest entries when we exceed our limit
            var oldestKeys = ProcessedTimestamps.Keys
                .OrderBy(k => k)
                .Take(ProcessedTimestamps.Count - MaxStoredTimestamps)
                .ToList();

            foreach (var key in oldestKeys)
            {
                ProcessedTimestamps.Remove(key);
            }
        }
    }
}