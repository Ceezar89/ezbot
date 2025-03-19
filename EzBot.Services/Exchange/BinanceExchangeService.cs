using EzBot.Models;
using EzBot.Common;
using EzBot.Services.Response;
using System.Text.Json;
using System.Globalization;

namespace EzBot.Services.Exchange;

public class BinanceExchangeService(HttpClient httpClient) : ICryptoExchangeService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly BinanceAdapter _adapter = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
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

    public async Task<bool> ExecuteTradeAsync(string symbol, double quantity, TradeType tradeType, CancellationToken cancellationToken)
    {
        if (tradeType == TradeType.None)
            return false;

        try
        {
            // Create a dictionary of parameters for the request
            var parameters = new Dictionary<string, string>
            {
                ["symbol"] = symbol,
                ["side"] = _adapter.MapTradeType(tradeType),
                ["type"] = _adapter.MapOrderType(),
                ["quantity"] = quantity.ToString(CultureInfo.InvariantCulture),
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
            };

            // TODO: When implementing with actual API keys, add security parameters:
            // - API Key (X-MBX-APIKEY header)
            // - Signature (HMAC SHA256)

            // Build query string from parameters
            var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}"));

            // Use the test endpoint for testing purposes, in production this would be controlled by a config setting
            var endpoint = _adapter.GetTestOrderEndpoint();

            // Create and send request
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri($"{endpoint}?{queryString}")
            };

            var response = await NetworkUtility.MakeRequestAsync(_httpClient, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException($"Failed to execute trade on Binance: {response.StatusCode}, Error: {errorContent}");
            }

            // Parse response to BinanceOrderResponse object
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var orderResponse = JsonSerializer.Deserialize<EzBot.Services.Response.BinanceOrderResponse>(responseContent, _jsonOptions);

            // Check if the order was successfully created
            return orderResponse?.OrderId > 0;
        }
        catch (Exception ex)
        {
            // Log error
            Console.WriteLine($"Error executing trade: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Query order information for a specific order
    /// </summary>
    /// <param name="symbol">Trading pair symbol</param>
    /// <param name="orderId">Order ID to query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Order information or null if not found</returns>
    public async Task<object?> QueryOrderAsync(string symbol, long orderId, CancellationToken cancellationToken)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["symbol"] = symbol,
                ["orderId"] = orderId.ToString(),
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
            };

            var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}"));
            var endpoint = _adapter.GetQueryOrderEndpoint();

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"{endpoint}?{queryString}")
            };

            var response = await NetworkUtility.MakeRequestAsync(_httpClient, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"Failed to query order on Binance: {response.StatusCode}, Error: {errorContent}");
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var orderResponse = JsonSerializer.Deserialize<EzBot.Services.Response.BinanceOrderResponse>(responseContent, _jsonOptions);

            return orderResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error querying order: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Change the leverage for a specific symbol
    /// </summary>
    /// <param name="symbol">Trading pair symbol</param>
    /// <param name="leverage">Leverage value (1-125)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> ChangeLeverageAsync(string symbol, int leverage, CancellationToken cancellationToken)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["symbol"] = symbol,
                ["leverage"] = leverage.ToString(),
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
            };

            var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}"));
            var endpoint = _adapter.GetLeverageEndpoint();

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri($"{endpoint}?{queryString}")
            };

            var response = await NetworkUtility.MakeRequestAsync(_httpClient, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"Failed to change leverage on Binance: {response.StatusCode}, Error: {errorContent}");
                return false;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var leverageResponse = JsonSerializer.Deserialize<EzBot.Services.Response.BinanceLeverageResponse>(responseContent, _jsonOptions);

            return leverageResponse?.Leverage == leverage;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error changing leverage: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Change the margin type for a specific symbol (ISOLATED or CROSSED)
    /// </summary>
    /// <param name="symbol">Trading pair symbol</param>
    /// <param name="marginType">Margin type (ISOLATED or CROSSED)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> ChangeMarginTypeAsync(string symbol, string marginType, CancellationToken cancellationToken)
    {
        try
        {
            // Always ensure we're using ISOLATED as per requirements
            var effectiveMarginType = _adapter.MapMarginType(marginType);

            var parameters = new Dictionary<string, string>
            {
                ["symbol"] = symbol,
                ["marginType"] = effectiveMarginType,
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
            };

            var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}"));
            var endpoint = _adapter.GetMarginTypeEndpoint();

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri($"{endpoint}?{queryString}")
            };

            var response = await NetworkUtility.MakeRequestAsync(_httpClient, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"Failed to change margin type on Binance: {response.StatusCode}, Error: {errorContent}");
                return false;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var marginTypeResponse = JsonSerializer.Deserialize<EzBot.Services.Response.BinanceMarginTypeResponse>(responseContent, _jsonOptions);

            return marginTypeResponse?.Code == 200;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error changing margin type: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Change the position mode (Dual or Hedge)
    /// </summary>
    /// <param name="dualSidePosition">True for Hedge Mode (dual), False for One-way Mode</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> ChangePositionModeAsync(bool dualSidePosition, CancellationToken cancellationToken)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["dualSidePosition"] = dualSidePosition.ToString().ToLowerInvariant(),
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
            };

            var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}"));
            var endpoint = _adapter.GetPositionModeEndpoint();

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri($"{endpoint}?{queryString}")
            };

            var response = await NetworkUtility.MakeRequestAsync(_httpClient, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"Failed to change position mode on Binance: {response.StatusCode}, Error: {errorContent}");
                return false;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var positionModeResponse = JsonSerializer.Deserialize<EzBot.Services.Response.BinancePositionModeResponse>(responseContent, _jsonOptions);

            return positionModeResponse?.Code == 200;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error changing position mode: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Cancel a specific order
    /// </summary>
    /// <param name="symbol">Trading pair symbol</param>
    /// <param name="orderId">Order ID to cancel</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> CancelOrderAsync(string symbol, long orderId, CancellationToken cancellationToken)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["symbol"] = symbol,
                ["orderId"] = orderId.ToString(),
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
            };

            var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}"));
            var endpoint = _adapter.GetCancelOrderEndpoint();

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Delete,
                RequestUri = new Uri($"{endpoint}?{queryString}")
            };

            var response = await NetworkUtility.MakeRequestAsync(_httpClient, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"Failed to cancel order on Binance: {response.StatusCode}, Error: {errorContent}");
                return false;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var orderResponse = JsonSerializer.Deserialize<EzBot.Services.Response.BinanceOrderResponse>(responseContent, _jsonOptions);

            return orderResponse?.OrderId > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error canceling order: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Cancel all open orders for a specific symbol
    /// </summary>
    /// <param name="symbol">Trading pair symbol</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> CancelAllOrdersAsync(string symbol, CancellationToken cancellationToken)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["symbol"] = symbol,
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
            };

            var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}"));
            var endpoint = _adapter.GetCancelAllOrdersEndpoint();

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Delete,
                RequestUri = new Uri($"{endpoint}?{queryString}")
            };

            var response = await NetworkUtility.MakeRequestAsync(_httpClient, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"Failed to cancel all orders on Binance: {response.StatusCode}, Error: {errorContent}");
                return false;
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error canceling all orders: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get position information for a specific symbol or all symbols
    /// </summary>
    /// <param name="symbol">Trading pair symbol (optional, null for all symbols)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of position information</returns>
    public async Task<object?> GetPositionInformationAsync(string? symbol, CancellationToken cancellationToken)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
            };

            if (!string.IsNullOrEmpty(symbol))
            {
                parameters.Add("symbol", symbol);
            }

            var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}"));
            var endpoint = _adapter.GetPositionInfoEndpoint();

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"{endpoint}?{queryString}")
            };

            var response = await NetworkUtility.MakeRequestAsync(_httpClient, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"Failed to get position information from Binance: {response.StatusCode}, Error: {errorContent}");
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var positions = JsonSerializer.Deserialize<List<EzBot.Services.Response.BinancePositionInfoResponse>>(responseContent, _jsonOptions);

            return positions;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting position information: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Get account balance information
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of account balances</returns>
    public async Task<object?> GetAccountBalanceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
            };

            var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}"));
            var endpoint = _adapter.GetAccountBalanceEndpoint();

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"{endpoint}?{queryString}")
            };

            var response = await NetworkUtility.MakeRequestAsync(_httpClient, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"Failed to get account balance from Binance: {response.StatusCode}, Error: {errorContent}");
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var balances = JsonSerializer.Deserialize<List<EzBot.Services.Response.BinanceAccountBalanceResponse>>(responseContent, _jsonOptions);

            return balances;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting account balance: {ex.Message}");
            return null;
        }
    }
}