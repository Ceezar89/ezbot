using EzBot.Core.Indicator;
using EzBot.Models;
using EzBot.Core.IndicatorParameter;

namespace EzBot.Tests.Core.Indicator;

public class TrendiloTests
{
    private static TrendiloParameter CreateParameter(
        int smoothing = 1,
        int lookback = 10,
        double almaOffset = 0.9,
        int almaSigma = 10,
        double bandMultiplier = 1.0)
    {
        // Assuming TrendiloParameter has an appropriate constructor or properties.
        // Replace this with the real constructor if available.
        return new TrendiloParameter("test", smoothing, lookback, almaOffset, almaSigma, bandMultiplier);
    }

    [Fact(Skip = "NotImplemented")]
    public void Calculate_BullishTrend_ReturnsBullish()
    {
        // Arrange: increasing closing prices.
        var bars = new List<BarData>();
        int startValue = 10000;
        for (int i = 0; i < 100; i++)
        {
            bars.Add(new BarData { Close = startValue + (i * 200) });
        }
        var parameter = CreateParameter();
        var trendilo = new Trendilo(parameter);

        // Act
        trendilo.SetBarData(new BarDataCollection(bars));
        var trend = trendilo.GetTrendSignal();

        // Assert: expect bullish if ALMA of percentage changes is much positive.
        Assert.Equal(TrendSignal.Bullish, trend);
    }

    [Fact(Skip = "NotImplemented")]
    public void Calculate_BearishTrend_ReturnsBearish()
    {
        // Arrange: decreasing closing prices.
        var bars = new List<BarData>
            {
                new() { Close = 5 },
                new() { Close = 4 },
                new() { Close = 3 },
                new() { Close = 2 },
                new() { Close = 1 }
            };
        var parameter = CreateParameter(smoothing: 1, lookback: 3);
        var trendilo = new Trendilo(parameter);

        // Act
        trendilo.SetBarData(new BarDataCollection(bars));
        var trend = trendilo.GetTrendSignal();

        // Assert: expect bearish if ALMA of percentage changes is sufficiently negative.
        Assert.Equal(TrendSignal.Bearish, trend);
    }

    [Fact(Skip = "NotImplemented")]
    public void Calculate_NeutralTrend_ReturnsNeutral()
    {
        // Arrange: constant closing prices.
        var bars = new List<BarData>
            {
                new() { Close = 10 },
                new() { Close = 10 },
                new() { Close = 10 },
                new() { Close = 10 },
                new() { Close = 10 }
            };
        var parameter = CreateParameter(smoothing: 1, lookback: 3);
        var trendilo = new Trendilo(parameter);

        // Act
        trendilo.SetBarData(new BarDataCollection(bars));
        var trend = trendilo.GetTrendSignal();

        // Assert: expect neutral signal since no change.
        Assert.Equal(TrendSignal.Neutral, trend);
    }
}
