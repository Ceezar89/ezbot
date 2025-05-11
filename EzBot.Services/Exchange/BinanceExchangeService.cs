using EzBot.Models;
using EzBot.Common;
using EzBot.Services.Response;
using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EzBot.Services.Exchange;

public class BinanceExchangeService : ICryptoExchangeService
{
    private readonly HttpClient _httpClient;
    private readonly BinanceAdapter _adapter = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    public static ExchangeName ExchangeName => ExchangeName.BINANCE;

    public string? ApiSecret { get; set; }

    public BinanceExchangeService(HttpClient httpClient, string? apiSecret = null, bool useTestnet = false)
    {
        _httpClient = httpClient;
        ApiSecret = apiSecret;
        _adapter.UseTestnet = useTestnet;
    }

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

            // Build query string from parameters
            var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}"));

            // Add signature if ApiSecret is provided
            if (!string.IsNullOrEmpty(ApiSecret))
            {
                var signature = GenerateSignature(queryString, ApiSecret);
                queryString = $"{queryString}&signature={signature}";
            }

            // Get the endpoint for order operations
            var endpoint = _adapter.GetTestOrderEndpoint();

            // Create and send request
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri($"{endpoint}?{queryString}")
            };

            Console.WriteLine($"Making request to {request.RequestUri}");
            var response = await NetworkUtility.MakeRequestAsync(_httpClient, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException($"Failed to execute trade on Binance: {response.StatusCode}, Error: {errorContent}");
            }

            // Parse response to BinanceOrderResponse object
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"Response from Binance: {responseContent}");

            // For real orders, try to get the order ID
            var orderResponse = JsonSerializer.Deserialize<EzBot.Services.Response.BinanceOrderResponse>(responseContent, _jsonOptions);

            // Log order details
            if (orderResponse != null)
            {
                Console.WriteLine($"Order created: ID={orderResponse.OrderId}, ClientOrderId={orderResponse.ClientOrderId}, Status={orderResponse.Status}");
            }

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
    /// Generate HMAC SHA256 signature for API request
    /// </summary>
    private static string GenerateSignature(string queryString, string apiSecret)
    {
        try
        {
            var key = Encoding.UTF8.GetBytes(apiSecret);
            var message = Encoding.UTF8.GetBytes(queryString);

            using var hmac = new HMACSHA256(key);
            var hash = hmac.ComputeHash(message);

            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating signature: {ex.Message}");
            throw;
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
        // Create a single-attempt function with fresh timestamp
        async Task<HttpResponseMessage> MakeSingleRequest()
        {
            try
            {
                // Create parameters with a fresh timestamp each time
                var parameters = new Dictionary<string, string>
                {
                    ["symbol"] = symbol,
                    ["leverage"] = leverage.ToString(),
                    ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
                    ["recvWindow"] = "5000" // Allow 5 seconds window for request processing
                };

                var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}"));

                // Add signature if ApiSecret is provided
                if (!string.IsNullOrEmpty(ApiSecret))
                {
                    var signature = GenerateSignature(queryString, ApiSecret);
                    queryString = $"{queryString}&signature={signature}";
                }

                var endpoint = _adapter.GetLeverageEndpoint();

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"{endpoint}?{queryString}")
                };

                Console.WriteLine($"Making leverage request to {request.RequestUri}");
                return await _httpClient.SendAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in request: {ex.Message}");
                throw;
            }
        }

        // Implement our own retry logic with fresh timestamps
        HttpResponseMessage? response = null;
        var maxRetries = 3;

        for (int retry = 0; retry <= maxRetries; retry++)
        {
            if (retry > 0)
            {
                Console.WriteLine($"Retrying leverage change... Attempt {retry}");
                // Add exponential backoff
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retry)), cancellationToken);
            }

            try
            {
                response = await MakeSingleRequest();

                // Success case - no need to retry
                if (response.IsSuccessStatusCode)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                if (retry == maxRetries)
                {
                    Console.WriteLine($"Max retries exceeded: {ex.Message}");
                    return false;
                }
            }
        }

        if (response == null)
        {
            return false;
        }

        try
        {
            // Read the response content
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"Leverage response from Binance: {responseContent}");

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to change leverage on Binance: {response.StatusCode}");
                // Some error responses may have a JSON structure
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<BinanceGenericResponse>(responseContent, _jsonOptions);
                    if (errorResponse != null)
                    {
                        Console.WriteLine($"Error code: {errorResponse.Code}, Message: {errorResponse.Message}");

                        // Special case: If current leverage is already what we're trying to set
                        if (errorResponse.Message.Contains("already"))
                        {
                            Console.WriteLine($"Leverage is already set to {leverage} - considering this a success");
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to parse error response: {ex.Message}");
                }

                return false;
            }

            // Success case
            var leverageResponse = JsonSerializer.Deserialize<BinanceLeverageResponse>(responseContent, _jsonOptions);
            return leverageResponse?.Leverage == leverage;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing response: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Change the margin type for a specific symbol (ISOLATED or CROSSED) - Not implemented
    /// </summary>
    /// <param name="symbol">Trading pair symbol</param>
    /// <param name="marginType">Margin type (ISOLATED or CROSSED)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>False (Not implemented)</returns>
    public async Task<bool> ChangeMarginTypeAsync(string symbol, string marginType, CancellationToken cancellationToken)
    {
        Console.WriteLine("ChangeMarginTypeAsync is not implemented");
        return await Task.FromResult(false);
    }

    /// <summary>
    /// Change the position mode (Dual or Hedge) - Not implemented
    /// </summary>
    /// <param name="dualSidePosition">True for Hedge Mode (dual), False for One-way Mode</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>False (Not implemented)</returns>
    public async Task<bool> ChangePositionModeAsync(bool dualSidePosition, CancellationToken cancellationToken)
    {
        Console.WriteLine("ChangePositionModeAsync is not implemented");
        return await Task.FromResult(false);
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

            // Add signature if ApiSecret is provided
            if (!string.IsNullOrEmpty(ApiSecret))
            {
                var signature = GenerateSignature(queryString, ApiSecret);
                queryString = $"{queryString}&signature={signature}";
            }

            var endpoint = _adapter.GetPositionInfoEndpoint();

            // Create and send request
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"{endpoint}?{queryString}")
            };

            Console.WriteLine($"Making position request to {request.RequestUri}");
            var response = await NetworkUtility.MakeRequestAsync(_httpClient, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"Failed to get position information from Binance: {response.StatusCode}, Error: {errorContent}");
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"Position response from Binance: {responseContent}");
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

            // Add signature if ApiSecret is provided
            if (!string.IsNullOrEmpty(ApiSecret))
            {
                var signature = GenerateSignature(queryString, ApiSecret);
                queryString = $"{queryString}&signature={signature}";
            }

            var endpoint = _adapter.GetAccountBalanceEndpoint();

            // Create and send request
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"{endpoint}?{queryString}")
            };

            Console.WriteLine($"Making balance request to {request.RequestUri}");
            var response = await NetworkUtility.MakeRequestAsync(_httpClient, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"Failed to get account balance from Binance: {response.StatusCode}, Error: {errorContent}");
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"Balance response from Binance: {responseContent}");
            var balances = JsonSerializer.Deserialize<List<EzBot.Services.Response.BinanceAccountBalanceResponse>>(responseContent, _jsonOptions);

            return balances;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting account balance: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Get all open orders for a symbol
    /// </summary>
    /// <param name="symbol">Trading pair symbol</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of open orders</returns>
    public async Task<List<BinanceOpenOrdersResponse>> GetOpenOrdersAsync(string symbol, CancellationToken cancellationToken)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["symbol"] = symbol,
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
            };

            // Build query string from parameters
            var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}"));

            // Add signature if ApiSecret is provided
            if (!string.IsNullOrEmpty(ApiSecret))
            {
                var signature = GenerateSignature(queryString, ApiSecret);
                queryString = $"{queryString}&signature={signature}";
            }

            // Get the endpoint for open orders
            var endpoint = _adapter.GetOpenOrdersEndpoint();

            // Create and send request
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"{endpoint}?{queryString}")
            };

            Console.WriteLine($"Making request to {request.RequestUri}");
            var response = await NetworkUtility.MakeRequestAsync(_httpClient, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"Failed to get open orders from Binance: {response.StatusCode}, Error: {errorContent}");
                return new List<BinanceOpenOrdersResponse>();
            }

            // Parse response to a list of BinanceOpenOrdersResponse objects
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"Response from Binance: {responseContent}");

            var openOrders = JsonSerializer.Deserialize<List<BinanceOpenOrdersResponse>>(responseContent, _jsonOptions);
            return openOrders ?? new List<BinanceOpenOrdersResponse>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting open orders: {ex.Message}");
            return new List<BinanceOpenOrdersResponse>();
        }
    }
}