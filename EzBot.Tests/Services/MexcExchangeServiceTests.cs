using EzBot.Services.Exchange;
using RichardSzalay.MockHttp;
using EzBot.Models;

namespace EzBot.Tests.Services
{
    public class MexcExchangeServiceTests
    {
        // dotnet test --filter "FullyQualifiedName~MexcExchangeServiceTests.GetBarDataAsync_ReturnsListOfBars"
        [Fact]
        public async Task GetBarDataAsync_ReturnsListOfBars()
        {
            // Arrange
            string jsonFilePath = Path.Combine(AppContext.BaseDirectory, "Services", "Inputs", "mexc_response.json");
            var jsonResponse = await File.ReadAllTextAsync(jsonFilePath);

            var adapter = new MexcAdapter();
            var expectedUrl = adapter.GetKlineRequestUri(CoinPair.BTCUSDT, Interval.OneHour);

            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When(expectedUrl)
                    .Respond("application/json", jsonResponse);

            var client = new HttpClient(mockHttp);
            var service = new MexcExchangeService(client);

            // Act
            var bars = await service.GetBarDataAsync(CoinPair.BTCUSDT, Interval.OneHour, default);

            // Assert
            Assert.Equal(2, bars.Count);

            Assert.Equal(1738256400, bars[0].TimeStamp);
            Assert.Equal(105657.3, bars[0].Open, 2);
            Assert.Equal(105657.4, bars[0].High, 2);
            Assert.Equal(104850.0, bars[0].Low, 2);
            Assert.Equal(105240.0, bars[0].Close, 2);
            Assert.Equal(20492413, bars[0].Volume, 0);

            Assert.Equal(1738260000, bars[1].TimeStamp);
            Assert.Equal(105240.0, bars[1].Open, 2);
            Assert.Equal(105537.6, bars[1].High, 2);
            Assert.Equal(104704.1, bars[1].Low, 2);
            Assert.Equal(105523.3, bars[1].Close, 2);
            Assert.Equal(15164388, bars[1].Volume, 0);
        }

        // dotnet test --filter "FullyQualifiedName~MexcExchangeServiceTests.GetKlineEndpointUrl_ReturnsSuccess"
        [Fact]
        public async Task GetKlineEndpointUrl_ReturnsSuccess()
        {
            // Arrange
            var adapter = new MexcAdapter();
            string url = adapter.GetKlineRequestUri(CoinPair.BTCUSDT, Interval.OneHour);
            using var httpClient = new HttpClient();

            // Act
            var response = await httpClient.GetAsync(url);

            // Assert
            Assert.True(response.IsSuccessStatusCode, $"Request to {url} failed with status {response.StatusCode}");
        }

        // dotnet test --filter "FullyQualifiedName~MexcExchangeServiceTests.GetBarDataAsync_Integration_ReturnsData"
        [Fact]
        public async Task GetBarDataAsync_Integration_ReturnsData()
        {
            // Arrange
            using var httpClient = new HttpClient();
            var service = new MexcExchangeService(httpClient);

            // Act
            var bars = await service.GetBarDataAsync(CoinPair.BTCUSDT, Interval.OneHour, CancellationToken.None);

            // Assert
            Assert.NotNull(bars);
            Assert.True(bars.Count > 0, "No bar data was returned from the service.");
        }
    }
}