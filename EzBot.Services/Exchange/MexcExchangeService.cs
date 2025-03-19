using EzBot.Models;
using EzBot.Common;
using EzBot.Services.Response;
using System.Text.Json;
using System.Globalization;

namespace EzBot.Services.Exchange;

public class MexcExchangeService(HttpClient httpClient) : ICryptoExchangeService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly MexcAdapter _adapter = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    public static ExchangeName ExchangeName => ExchangeName.MEXC;

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
            throw new HttpRequestException($"Failed to retrieve bar data from MEXC: {response.StatusCode}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var klineResponse = JsonSerializer.Deserialize<EzBot.Services.Response.MexcKlineResponse>(responseContent, _jsonOptions);

        if (klineResponse == null)
            return [];

        var data = klineResponse.Data;
        if (data.Time.Length == 0)
            return [];

        var barDataList = new List<BarData>();
        var times = data.Time;
        var opens = data.Open;
        var highs = data.High;
        var lows = data.Low;
        var closes = data.Close;
        var volumes = data.Vol;

        for (int i = 0; i < times.Length; i++)
        {
            barDataList.Add(new BarData
            {
                TimeStamp = times[i],
                Open = opens[i],
                High = highs[i],
                Low = lows[i],
                Close = closes[i],
                Volume = volumes[i]
            });
        }

        return barDataList;
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
                ["vol"] = quantity.ToString(CultureInfo.InvariantCulture),
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
            };

            // TODO: When implementing with actual API keys, add security parameters:
            // - API Key (X-MBX-APIKEY header)
            // - Signature (HMAC SHA256)

            // Build query string from parameters
            var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}"));

            // Use the test endpoint for testing purposes
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
                Console.WriteLine($"Failed to execute trade on MEXC: {response.StatusCode}, Error: {errorContent}");
                return false;
            }

            // Parse response
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var orderResponse = JsonSerializer.Deserialize<EzBot.Services.Response.MexcOrderResponse>(responseContent, _jsonOptions);

            // Check if the order was successfully created
            return orderResponse?.Code == 0 && !string.IsNullOrEmpty(orderResponse?.Data?.OrderId);
        }
        catch (Exception ex)
        {
            // Log error
            Console.WriteLine($"Error executing trade on MEXC: {ex.Message}");
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
                ["order_id"] = orderId.ToString(),
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
                Console.WriteLine($"Failed to query order on MEXC: {response.StatusCode}, Error: {errorContent}");
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var orderResponse = JsonSerializer.Deserialize<EzBot.Services.Response.MexcOrderResponse>(responseContent, _jsonOptions);

            return orderResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error querying order on MEXC: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Change the leverage for a specific symbol
    /// </summary>
    /// <param name="symbol">Trading pair symbol</param>
    /// <param name="leverage">Leverage value</param>
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
                Console.WriteLine($"Failed to change leverage on MEXC: {response.StatusCode}, Error: {errorContent}");
                return false;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var baseResponse = JsonSerializer.Deserialize<EzBot.Services.Response.MexcBaseResponse>(responseContent, _jsonOptions);

            return baseResponse?.Code == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error changing leverage on MEXC: {ex.Message}");
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
                ["isolated"] = effectiveMarginType,
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
                Console.WriteLine($"Failed to change margin type on MEXC: {response.StatusCode}, Error: {errorContent}");
                return false;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var baseResponse = JsonSerializer.Deserialize<EzBot.Services.Response.MexcBaseResponse>(responseContent, _jsonOptions);

            return baseResponse?.Code == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error changing margin type on MEXC: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Change the position mode (Dual or One-way)
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
                ["position_mode"] = dualSidePosition ? "2" : "1", // 1 for one-way, 2 for hedge mode
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
                Console.WriteLine($"Failed to change position mode on MEXC: {response.StatusCode}, Error: {errorContent}");
                return false;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var baseResponse = JsonSerializer.Deserialize<EzBot.Services.Response.MexcBaseResponse>(responseContent, _jsonOptions);

            return baseResponse?.Code == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error changing position mode on MEXC: {ex.Message}");
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
                ["order_id"] = orderId.ToString(),
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
            };

            var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}"));
            var endpoint = _adapter.GetCancelOrderEndpoint();

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri($"{endpoint}?{queryString}")
            };

            var response = await NetworkUtility.MakeRequestAsync(_httpClient, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"Failed to cancel order on MEXC: {response.StatusCode}, Error: {errorContent}");
                return false;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var baseResponse = JsonSerializer.Deserialize<EzBot.Services.Response.MexcBaseResponse>(responseContent, _jsonOptions);

            return baseResponse?.Code == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error canceling order on MEXC: {ex.Message}");
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
                Method = HttpMethod.Post,
                RequestUri = new Uri($"{endpoint}?{queryString}")
            };

            var response = await NetworkUtility.MakeRequestAsync(_httpClient, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"Failed to cancel all orders on MEXC: {response.StatusCode}, Error: {errorContent}");
                return false;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var baseResponse = JsonSerializer.Deserialize<EzBot.Services.Response.MexcBaseResponse>(responseContent, _jsonOptions);

            return baseResponse?.Code == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error canceling all orders on MEXC: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get position information for a specific symbol or all symbols
    /// </summary>
    /// <param name="symbol">Trading pair symbol (optional, null for all symbols)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Position information</returns>
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
                Console.WriteLine($"Failed to get position information from MEXC: {response.StatusCode}, Error: {errorContent}");
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var positionResponse = JsonSerializer.Deserialize<EzBot.Services.Response.MexcPositionResponse>(responseContent, _jsonOptions);

            return positionResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting position information from MEXC: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Get account balance information
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Account balance information</returns>
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
                Console.WriteLine($"Failed to get account balance from MEXC: {response.StatusCode}, Error: {errorContent}");
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var accountResponse = JsonSerializer.Deserialize<EzBot.Services.Response.MexcAccountResponse>(responseContent, _jsonOptions);

            return accountResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting account balance from MEXC: {ex.Message}");
            return null;
        }
    }
}
