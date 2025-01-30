using EzBot.Models;
using EzBot.Common;
using System.Text.Json;
using EzBot.Services.DTO;

namespace EzBot.Services;

public class BinanceExchangeService(HttpClient httpClient) : ICryptoExchangeService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly IExchangeAdapter _adapter = new BinanceAdapter();
    public static ExchangeName ExchangeName => ExchangeName.BINANCE;

    // https://developers.binance.com/docs/derivatives/usds-margined-futures/market-data/rest-api/Kline-Candlestick-Data
    public async Task<List<BarData>> GetBarDataAsync(CoinPair symbol, Interval interval, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"{_adapter.GetBaseUrl()}{_adapter.GetKlineEndpoint()}?symbol={_adapter.MapSymbol(symbol)}&interval={_adapter.MapInterval(interval)}"),
        };

        var response = await NetworkUtility.MakeRequestAsync(_httpClient, request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var klines = await JsonSerializer.DeserializeAsync<List<BinanceKlineResponse>>(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            return klines?.Select(k => new BarData
            {
                TimeStamp = (int)k.OpenTime,
                Open = double.Parse(k.Open),
                High = double.Parse(k.High),
                Low = double.Parse(k.Low),
                Close = double.Parse(k.Close),
                Volume = double.Parse(k.Volume)
            }).ToList() ?? [];
        }
        throw new HttpRequestException($"Failed to retrieve bar data from Binance: {response.StatusCode}");
    }

    public async Task<bool> ExecuteTradeAsync(string symbol, double quantity, TradeType tradeType, CancellationToken cancellationToken)
    {
        // TODO: Implement trade execution
        return true; // stub
    }
}