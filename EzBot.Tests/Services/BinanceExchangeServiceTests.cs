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
        string jsonFilePath = Path.Combine(AppContext.BaseDirectory, "Services", "Inputs", "binance_response.json");
        var jsonResponse = await File.ReadAllTextAsync(jsonFilePath);

        var adapter = new BinanceAdapter();
        var expectedUrl = adapter.GetKlineRequestUri(CoinPair.BTCUSDT, Interval.OneHour);

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(expectedUrl)
                .Respond("application/json", jsonResponse);

        var client = new HttpClient(mockHttp);
        var service = new BinanceExchangeService(client);

        // Act
        var bars = await service.GetBarDataAsync(CoinPair.BTCUSDT, Interval.OneHour, default);

        // Assert
        Assert.Equal(2, bars.Count);
        Assert.Equal(1736611200000, bars[0].TimeStamp);
        Assert.Equal(94365.90, bars[0].Open);
        Assert.Equal(94680.30, bars[0].High);
        Assert.Equal(94251.00, bars[0].Low);
        Assert.Equal(94410.00, bars[0].Close);
        Assert.Equal(3634.879, bars[0].Volume);

        Assert.Equal(1736614800000, bars[1].TimeStamp);
        Assert.Equal(94410.10, bars[1].Open);
        Assert.Equal(94414.40, bars[1].High);
        Assert.Equal(93943.00, bars[1].Low);
        Assert.Equal(94116.40, bars[1].Close);
        Assert.Equal(5335.771, bars[1].Volume);
    }

    // dotnet test --filter "FullyQualifiedName~BinanceExchangeServiceTests.GetKlineEndpointUrl_ReturnsSuccess"
    [Fact]
    public async Task GetKlineEndpointUrl_ReturnsSuccess()
    {
        // Arrange
        var adapter = new BinanceAdapter();
        string url = adapter.GetKlineRequestUri(CoinPair.BTCUSDT, Interval.OneHour);
        using var httpClient = new HttpClient();

        // Act
        var response = await httpClient.GetAsync(url);

        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Request to {url} failed with status {response.StatusCode}");
    }

    // dotnet test --filter "FullyQualifiedName~BinanceExchangeServiceTests.GetBarDataAsync_Integration_ReturnsData"
    [Fact]
    public async Task GetBarDataAsync_Integration_ReturnsData()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var service = new BinanceExchangeService(httpClient);

        // Act
        var bars = await service.GetBarDataAsync(CoinPair.BTCUSDT, Interval.OneHour, CancellationToken.None);

        // Assert
        Assert.NotNull(bars);
        Assert.True(bars.Count > 0, "No bar data was returned from the service.");
    }
}