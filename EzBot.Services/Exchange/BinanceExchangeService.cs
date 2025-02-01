using EzBot.Models;
using EzBot.Common;
using System.Text.Json;
using System.Globalization;

namespace EzBot.Services.Exchange;

public class BinanceExchangeService(HttpClient httpClient) : ICryptoExchangeService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly BinanceAdapter _adapter = new();
    public static ExchangeName ExchangeName => ExchangeName.BINANCE;

    // https://developers.binance.com/docs/derivatives/usds-margined-futures/market-data/rest-api/Kline-Candlestick-Data
    public async Task<List<BarData>> GetBarDataAsync(CoinPair symbol, Interval interval, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(_adapter.GetKlineRequestUri(symbol, interval))
        };

        var response = await NetworkUtility.MakeRequestAsync(_httpClient, request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to retrieve bar data from Binance: {response.StatusCode}");
        }

        var rawKlines = await JsonSerializer.DeserializeAsync<List<List<JsonElement>>>(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken
        );

        if (rawKlines is null)
            return [];

        return [.. rawKlines.Select(klineArray =>
        {
            var time = klineArray[0].GetInt64();
            var open = klineArray[1].GetString();
            var high = klineArray[2].GetString();
            var low = klineArray[3].GetString();
            var close = klineArray[4].GetString();
            var volume = klineArray[5].GetString();

            return new BarData
            {
                TimeStamp = time,
                Open = double.Parse(open!, CultureInfo.InvariantCulture),
                High = double.Parse(high!, CultureInfo.InvariantCulture),
                Low = double.Parse(low!, CultureInfo.InvariantCulture),
                Close = double.Parse(close!, CultureInfo.InvariantCulture),
                Volume = double.Parse(volume!, CultureInfo.InvariantCulture)
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