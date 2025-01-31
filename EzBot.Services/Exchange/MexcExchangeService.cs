using EzBot.Models;
using EzBot.Common;
using System.Text.Json;
using EzBot.Services.DTO;
using System.Globalization;

namespace EzBot.Services.Exchange;

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
            RequestUri = new Uri($"{_adapter.GetBaseUrl()}{_adapter.GetKlineEndpoint()}{_adapter.MapSymbol(symbol)}?interval={_adapter.MapInterval(interval)}"),
        };

        var response = await NetworkUtility.MakeRequestAsync(_httpClient, request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to retrieve bar data from Mexc: {response.StatusCode}");
        }

        var rawKlines = await JsonSerializer.DeserializeAsync<MexcKlineResponseDto>(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken
        );

        if (rawKlines is null)
            return [];

        return [.. rawKlines.Data.Time.Select((time, index) =>
        {
            var open = rawKlines.Data.Open[index];
            var high = rawKlines.Data.High[index];
            var low = rawKlines.Data.Low[index];
            var close = rawKlines.Data.Close[index];
            var volume = rawKlines.Data.Vol[index];

            // Return BarData
            return new BarData
            {
                TimeStamp = time,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume
            };
        })];

    }

    // public async Task<bool> ExecuteTradeAsync(string symbol, double quantity, TradeType tradeType, CancellationToken cancellationToken)
    public Task<bool> ExecuteTradeAsync(string symbol, double quantity, TradeType tradeType, CancellationToken cancellationToken)
    {
        // TODO: Implement trade execution
        return Task.FromResult(true); // stub
    }
}
