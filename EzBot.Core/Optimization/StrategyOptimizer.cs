using EzBot.Core.Indicator;
using EzBot.Models;

namespace EzBot.Core.Optimization
{
    public class StrategyOptimizer
    {
        public static HashSet<IndicatorCollection> GenerateExhaustiveIndicatorSet(IndicatorCollection initial)
        {
            var result = new HashSet<IndicatorCollection>();
            var queue = new Queue<IndicatorCollection>();

            // Start with the initial configuration.
            queue.Enqueue(initial);
            result.Add(initial);

            // Breadth-first search to generate all possible configurations.
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                for (int i = 0; i < current.Count; i++)
                {
                    var parameters = current[i].GetParameters();
                    if (parameters.CanIncrement())
                    {
                        // Clone the collection so that we do not modify the current state.
                        var clone = current.DeepClone();
                        // Increment the parameter for the indicator at position i.
                        var indicatorToUpdate = clone[i];
                        var newParams = indicatorToUpdate.GetParameters();
                        newParams.IncrementSingle();
                        indicatorToUpdate.UpdateParameters(newParams);

                        // If this configuration hasn’t been seen before, add it and enqueue.
                        if (result.Add(clone))
                        {
                            queue.Enqueue(clone);
                        }
                    }
                }
            }
            return result;
        }
    }
}
