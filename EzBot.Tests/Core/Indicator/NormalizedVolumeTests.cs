using EzBot.Core.Indicator;
using EzBot.Models;
using EzBot.Core.IndicatorParameter;

namespace EzBot.Tests.Core.Indicator;

public class NormalizedVolumeTests
{
    [Fact]
    public void Calculate_WhenLastVolumeIsAboveHighThreshold_ReturnsHighSignal()
    {
        // Arrange
        // 50 bars all with volume=1 
        var bars = Enumerable.Repeat(new BarData(0, 0, 0, 0, 0, 1), 50).ToList();
        // Then last bar with volume=2
        bars.Add(new BarData(0, 0, 0, 0, 0, 2));

        var indicator = new NormalizedVolume(new NormalizedVolumeParameter());
        // Act
        indicator.Calculate(bars);
        var result = indicator.GetVolumeSignal();

        // Assert
        Assert.Equal(VolumeSignal.High, result);
    }

    [Fact]
    public void Calculate_WhenLastVolumeIsBetweenNormalAndHigh_ReturnsNormalSignal()
    {
        // Arrange
        // Using defaults again (hv=150, lv=75, nv=100):
        // If the last bar volume is 120, the ratio = 120 / 100 * 100 = 120
        // -> That’s between 100 (NormalVolumeThreshold) and 150 (HighVolumeThreshold),
        // => Should return VolumeSignal.Normal.

        var bars = Enumerable.Repeat(new BarData { Volume = 100 }, 50).ToList();
        bars.Add(new BarData { Volume = 120 });

        var indicator = new NormalizedVolume(new NormalizedVolumeParameter());

        // Act
        indicator.Calculate(bars);
        var result = indicator.GetVolumeSignal();

        // Assert
        Assert.Equal(VolumeSignal.Normal, result);
    }

    [Fact]
    public void Calculate_WhenLastVolumeIsLow_ReturnsLowSignal()
    {
        // Arrange
        // If the last bar volume is 50, the ratio = 50 / 100 * 100 = 50
        // That’s below or equal to the LowVolumeThreshold (75), so this should return VolumeSignal.Low.

        var bars = Enumerable.Repeat(new BarData { Volume = 100 }, 50).ToList();
        bars.Add(new BarData { Volume = 50 });

        var indicator = new NormalizedVolume(new NormalizedVolumeParameter());

        // Act
        indicator.Calculate(bars);
        var result = indicator.GetVolumeSignal();

        // Assert
        Assert.Equal(VolumeSignal.Low, result);
    }

    [Fact]
    public void Calculate_WhenLastVolumeIsJustAboveNormalThreshold_ReturnsNormalSignal()
    {
        // Arrange
        // If the last bar volume is 101, ratio = 101 / 100 * 100 = 101
        // That’s greater than NormalVolumeThreshold (100) but below HighVolumeThreshold (150),
        // => Should return VolumeSignal.Normal.

        var bars = Enumerable.Repeat(new BarData { Volume = 100 }, 50).ToList();
        bars.Add(new BarData { Volume = 101 });

        var indicator = new NormalizedVolume(new NormalizedVolumeParameter());

        // Act
        indicator.Calculate(bars);
        var result = indicator.GetVolumeSignal();

        // Assert
        Assert.Equal(VolumeSignal.Normal, result);
    }
}
