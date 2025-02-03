
using EzBot.Core.Indicator;
using EzBot.Models;
using EzBot.Models.Indicator;

namespace EzBot.Tests.Core.Indicator;

public class EtmaTests
{
    [Fact]
    public void Calculate_Bullish_VeryStrong()
    {
        // Arrange: Create increasing bar data to trigger Bullish signal.
        var bars = new List<BarData>
            {
                new() { High = 10, Low = 10, Close = 10 },
                new() { High = 11, Low = 10, Close = 11 },
                new() { High = 12, Low = 10, Close = 12 },
                new() { High = 13, Low = 10, Close = 13 },
                new() { High = 14, Low = 10, Close = 14 }
            };

        var parameter = new EtmaParameter("test", 14, SignalStrength.VeryStrong);
        var etma = new Etma(parameter);

        // Act: Calculate the indicator.
        etma.Calculate(bars);
        var trend = etma.GetTrendSignal();

        // Assert: Expect Bullish trend.
        Assert.Equal(TrendSignal.Bullish, trend);
    }

    [Fact]
    public void Calculate_Bearish_Strong()
    {
        // Arrange: Create decreasing bar data to trigger Bearish signal.
        var bars = new List<BarData>
            {
                new() { High = 10, Low = 10, Close = 10 },
                new() { High = 9,  Low = 9,  Close = 9  },
                new() { High = 8,  Low = 8,  Close = 8  },
                new() { High = 7,  Low = 7,  Close = 7  },
                new() { High = 6,  Low = 6,  Close = 6  }
            };

        var parameter = new EtmaParameter("test", 14, SignalStrength.Strong);
        var etma = new Etma(parameter);

        // Act: Calculate the indicator.
        etma.Calculate(bars);
        var trend = etma.GetTrendSignal();

        // Assert: Expect Bearish trend.
        Assert.Equal(TrendSignal.Bearish, trend);
    }

    [Fact]
    public void Calculate_Neutral_Signal()
    {
        // Arrange: Create flat bar data to yield a Neutral signal.
        var bars = new List<BarData>
            {
                new() { High = 10, Low = 10, Close = 10 },
                new() { High = 10, Low = 10, Close = 10 },
                new() { High = 10, Low = 10, Close = 10 },
                new() { High = 10, Low = 10, Close = 10 },
                new() { High = 10, Low = 10, Close = 10 }
            };

        var parameter = new EtmaParameter("test", 14, SignalStrength.Signal);
        var etma = new Etma(parameter);

        // Act: Calculate the indicator.
        etma.Calculate(bars);
        var trend = etma.GetTrendSignal();

        // Assert: Expect Neutral trend.
        Assert.Equal(TrendSignal.Neutral, trend);
    }
}