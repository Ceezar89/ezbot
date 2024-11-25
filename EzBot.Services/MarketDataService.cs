using Microsoft.Extensions.Logging;

namespace EzBot.Services;

// EzBot.Services - MarketDataService.cs
public class MarketDataService
{
    private readonly Dictionary<string, decimal> _priceData = new();
    private readonly Timer _timer;
    private readonly IExchangeApi _exchangeApi;
    private readonly ILogger<MarketDataService> _logger;

    public MarketDataService(IExchangeApi exchangeApi, ILogger<MarketDataService> logger)
    {
        _exchangeApi = exchangeApi;
        _logger = logger;
        _timer = new Timer(FetchData, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    public decimal GetPrice(string asset)
    {
        return _priceData.TryGetValue(asset, out var price) ? price : 0m;
    }

    private async void FetchData(object state)
    {
        try
        {
            var assets = new List<string> { "BTC", "ETH" }; // Add more as needed
            foreach (var asset in assets)
            {
                var price = await _exchangeApi.GetPriceAsync(asset);
                _priceData[asset] = price;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching market data.");
        }
    }
}

