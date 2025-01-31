using EzBot.Services.Exchange;
using RichardSzalay.MockHttp;
using EzBot.Models;

namespace EzBot.Tests.Services;

public class BinanceExchangeServiceTests
{
    // dotnet test --filter "FullyQualifiedName~BinanceExchangeServiceTests.GetBarDataAsync_ReturnsListOfBars"
    [Fact]
    public async Task GetBarDataAsync_ReturnsListOfBars()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://fapi.binance.com/fapi/v1/klines*")
        .Respond("application/json", @"[
                [
                    1736517600000,
                    ""93522.10"",
                    ""94668.90"",
                    ""93377.60"",
                    ""94078.10"",
                    ""26858.873"",
                    1736521199999,
                    ""2525980534.94520"",
                    518586,
                    ""13622.277"",
                    ""1281350153.90430"",
                    ""0""
                ],
                [
                    1736521200000,
                    ""94078.10"",
                    ""94498.40"",
                    ""92173.60"",
                    ""93704.00"",
                    ""40000.228"",
                    1736524799999,
                    ""3732606360.79640"",
                    670479,
                    ""18953.990"",
                    ""1768901898.18170"",
                    ""0""
                ]
            ]");

        var client = new HttpClient(mockHttp);
        var service = new BinanceExchangeService(client);

        var bars = await service.GetBarDataAsync(CoinPair.BTCUSDT, Interval.OneHour, default);

        // Assert
        Assert.Equal(2, bars.Count);

        Assert.Equal(1736517600000, bars[0].TimeStamp);
        Assert.Equal(93522.10, bars[0].Open);
        Assert.Equal(94668.90, bars[0].High);
        Assert.Equal(93377.60, bars[0].Low);
        Assert.Equal(94078.10, bars[0].Close);
        Assert.Equal(26858.873, bars[0].Volume);

        Assert.Equal(1736521200000, bars[1].TimeStamp);
        Assert.Equal(94078.10, bars[1].Open);
        Assert.Equal(94498.40, bars[1].High);
        Assert.Equal(92173.60, bars[1].Low);
        Assert.Equal(93704.00, bars[1].Close);
        Assert.Equal(40000.228, bars[1].Volume);
    }
}