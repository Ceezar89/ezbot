using EzBot.Models;
using EzBot.Common;
using System.Text.Json;

namespace EzBot.Services;

public class MexcExchangeService(HttpClient httpClient) : ICryptoExchangeService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly IExchangeAdapter _adapter = new MexcAdapter();
    public static ExchangeName ExchangeName => ExchangeName.MEXC;
    /*
    Mexc API response format for candlestick data
    https://mexcdevelop.github.io/apidocs/contract_v1_en/#k-line-data
    {
        "success": true,
        "code": 0,
        "data": {
            "time": [
            1738256400,
            1738260000
            ],
            "open": [
            105657.3,
            105240.0
            ],
            "close": [
            105240.0,
            105523.3
            ],
            "high": [
            105657.4,
            105537.6
            ],
            "low": [
            104850.0,
            104704.1
            ],
            "vol": [
            2.0492413E7,
            1.5164388E7
            ],
            "amount": [
            2.1555558718113E8,
            1.594351956024E8
            ],
            "realOpen": [
            105657.3,
            105240.0
            ],
            "realClose": [
            105240.0,
            105523.3
            ],
            "realHigh": [
            105657.4,
            105537.6
            ],
            "realLow": [
            104850.0,
            104704.1
            ]
        }
    }
    */
    public async Task<List<BarData>> GetBarDataAsync(CoinPair symbol, Interval interval, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"{_adapter.GetBaseUrl()}{_adapter.GetKlineEndpoint()}{_adapter.MapSymbol(symbol)}&interval={_adapter.MapInterval(interval)}"),
        };

        var response = await NetworkUtility.MakeRequestAsync(_httpClient, request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            using JsonDocument document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            JsonElement root = document.RootElement.GetProperty("data");
            var barDataList = new List<BarData>();
            for (int i = 0; i < root.GetProperty("time").GetArrayLength(); i++)
            {
                barDataList.Add(new BarData
                {
                    TimeStamp = root.GetProperty("time")[i].GetInt32(),
                    Open = root.GetProperty("open")[i].GetDouble(),
                    High = root.GetProperty("high")[i].GetDouble(),
                    Low = root.GetProperty("low")[i].GetDouble(),
                    Close = root.GetProperty("close")[i].GetDouble(),
                    Volume = root.GetProperty("vol")[i].GetDouble()
                });
            }
            return barDataList;
        }
        throw new HttpRequestException($"Failed to retrieve bar data from Mexc: {response.StatusCode}");
    }

    public async Task<bool> ExecuteTradeAsync(string symbol, double quantity, TradeType tradeType, CancellationToken cancellationToken)
    {
        // ...similar logic to BinanceExchangeService...
        // Implement trade execution...
        return true; // placeholder
    }
}
