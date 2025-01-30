using EzBot.Models;
using EzBot.Common;
using System.Text.Json;

namespace EzBot.Services;

public class BinanceExchangeService(HttpClient httpClient) : ICryptoExchangeService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly IExchangeAdapter _adapter = new BinanceAdapter();
    public static ExchangeName ExchangeName => ExchangeName.BINANCE;

    /*
    Binance API response format for futures candlestick data
    https://developers.binance.com/docs/derivatives/usds-margined-futures/market-data/rest-api/Kline-Candlestick-Data
    [
        [
            1499040000000,      // Open time
            "0.01634790",       // Open
            "0.80000000",       // High
            "0.01575800",       // Low
            "0.01577100",       // Close
            "148976.11427815",  // Volume
            1499644799999,      // Close time
            "2434.19055334",    // Quote asset volume
            308,                // Number of trades
            "1756.87402397",    // Taker buy base asset volume
            "28.46694368",      // Taker buy quote asset volume
            "17928899.62484339" // Ignore.
        ]
    ]
    */
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
            using JsonDocument document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            JsonElement root = document.RootElement;
            var barDataList = new List<BarData>();
            foreach (JsonElement candle in root.EnumerateArray())
            {
                barDataList.Add(new BarData
                {
                    TimeStamp = candle[0].GetInt32(),
                    Open = candle[1].GetDouble(),
                    High = candle[2].GetDouble(),
                    Low = candle[3].GetDouble(),
                    Close = candle[4].GetDouble(),
                    Volume = candle[5].GetDouble()
                });
            }
            return barDataList;
        }
        throw new HttpRequestException($"Failed to retrieve bar data from Binance: {response.StatusCode}");
    }

    public async Task<bool> ExecuteTradeAsync(string symbol, double quantity, TradeType tradeType, CancellationToken cancellationToken)
    {
        // TODO: Implement trade execution
        return true; // stub
    }
}