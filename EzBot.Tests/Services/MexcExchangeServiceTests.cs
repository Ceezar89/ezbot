using EzBot.Services.Exchange;
using RichardSzalay.MockHttp;
using EzBot.Models;

namespace EzBot.Tests.Services;

public class MexcExchangeServiceTests
{
    // dotnet test --filter "FullyQualifiedName~MexcExchangeServiceTests.GetBarDataAsync_ReturnsListOfBars"
    [Fact]
    public async Task GetBarDataAsync_ReturnsListOfBars()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://contract.mexc.com/api/v1/contract/kline*")
            .Respond("application/json", @"{
                                                ""success"": true,
                                                ""code"": 0,
                                                ""data"": {
                                                    ""time"": [
                                                        1738256400,
                                                        1738260000
                                                    ],
                                                    ""open"": [
                                                        105657.3,
                                                        105240.0
                                                    ],
                                                    ""close"": [
                                                        105240.0,
                                                        105523.3
                                                    ],
                                                    ""high"": [
                                                        105657.4,
                                                        105537.6
                                                    ],
                                                    ""low"": [
                                                        104850.0,
                                                        104704.1
                                                    ],
                                                    ""vol"": [
                                                        2.0492413E7,
                                                        1.5164388E7
                                                    ],
                                                    ""amount"": [
                                                        2.1555558718113E8,
                                                        1.594351956024E8
                                                    ],
                                                    ""realOpen"": [
                                                        105657.3,
                                                        105240.0
                                                    ],
                                                    ""realClose"": [
                                                        105240.0,
                                                        105523.3
                                                    ],
                                                    ""realHigh"": [
                                                        105657.4,
                                                        105537.6
                                                    ],
                                                    ""realLow"": [
                                                        104850.0,
                                                        104704.1
                                                    ]
                                                }
                                            }");

        var client = new HttpClient(mockHttp);
        var service = new MexcExchangeService(client);

        // Act
        var bars = await service.GetBarDataAsync(CoinPair.BTCUSDT, Interval.OneHour, default);

        // Assert
        Assert.Equal(2, bars.Count);

        Assert.Equal(105657.3, bars[0].Open, 2);
        Assert.Equal(105657.4, bars[0].High, 2);
        Assert.Equal(104850.0, bars[0].Low, 2);
        Assert.Equal(105240.0, bars[0].Close, 2);
        Assert.Equal(20492413, bars[0].Volume, 0);

        Assert.Equal(105240.0, bars[1].Open, 2);
        Assert.Equal(105537.6, bars[1].High, 2);
        Assert.Equal(104704.1, bars[1].Low, 2);
        Assert.Equal(105523.3, bars[1].Close, 2);
        Assert.Equal(15164388, bars[1].Volume, 0);
    }
}