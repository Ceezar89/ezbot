using EzBot.Models;
using EzBot.Common;
using System.Text.Json;
using EzBot.Services.DTO;

namespace EzBot.Services;

public class MexcExchangeService(HttpClient httpClient) : ICryptoExchangeService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly IExchangeAdapter _adapter = new MexcAdapter();
    public static ExchangeName ExchangeName => ExchangeName.MEXC;

    // https://mexcdevelop.github.io/apidocs/contract_v1_en/#k-line-data
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
            var klines = await JsonSerializer.DeserializeAsync<List<MexcKlineResponse>>(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            return klines?.Select(k => new BarData
            {
                TimeStamp = (int)k.Time,
                Open = double.Parse(k.Open),
                High = double.Parse(k.High),
                Low = double.Parse(k.Low),
                Close = double.Parse(k.Close),
                Volume = double.Parse(k.Volume)
            }).ToList() ?? [];
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
