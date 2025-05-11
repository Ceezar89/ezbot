using EzBot.Services.Exchange;
using RichardSzalay.MockHttp;
using EzBot.Models;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using Xunit.Abstractions;
using Xunit;
using EzBot.Services.Response;

namespace EzBot.Tests.Services;

public class BinanceExchangeServiceTests
{
    // dotnet test --filter "FullyQualifiedName~BinanceExchangeServiceTests.GetBarDataAsync_ReturnsListOfBars"
    [Fact]
    public async Task GetBarDataAsync_ReturnsListOfBars()
    {
        // Arrange
        string jsonFilePath = Path.Combine(AppContext.BaseDirectory, "Services", "Inputs", "binance_response.json");
        var jsonResponse = await File.ReadAllTextAsync(jsonFilePath);

        var adapter = new BinanceAdapter();
        var expectedUrl = adapter.GetKlineRequestUri(CoinPair.BTCUSDT, Interval.OneHour);

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(expectedUrl)
                .Respond("application/json", jsonResponse);

        var client = new HttpClient(mockHttp);
        var service = new BinanceExchangeService(client);

        // Act
        var bars = await service.GetBarDataAsync(CoinPair.BTCUSDT, Interval.OneHour, default);

        // Assert
        Assert.Equal(2, bars.Count);
        Assert.Equal(1736611200000, bars[0].TimeStamp);
        Assert.Equal(94365.90, bars[0].Open);
        Assert.Equal(94680.30, bars[0].High);
        Assert.Equal(94251.00, bars[0].Low);
        Assert.Equal(94410.00, bars[0].Close);
        Assert.Equal(3634.879, bars[0].Volume);

        Assert.Equal(1736614800000, bars[1].TimeStamp);
        Assert.Equal(94410.10, bars[1].Open);
        Assert.Equal(94414.40, bars[1].High);
        Assert.Equal(93943.00, bars[1].Low);
        Assert.Equal(94116.40, bars[1].Close);
        Assert.Equal(5335.771, bars[1].Volume);
    }

    // dotnet test --filter "FullyQualifiedName~BinanceExchangeServiceTests.GetKlineEndpointUrl_ReturnsSuccess"
    [Fact]
    public async Task GetKlineEndpointUrl_ReturnsSuccess()
    {
        // Arrange
        var adapter = new BinanceAdapter();
        string url = adapter.GetKlineRequestUri(CoinPair.BTCUSDT, Interval.OneHour);
        using var httpClient = new HttpClient();

        // Act
        var response = await httpClient.GetAsync(url);

        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Request to {url} failed with status {response.StatusCode}");
    }

    // dotnet test --filter "FullyQualifiedName~BinanceExchangeServiceTests.GetBarDataAsync_Integration_ReturnsData"
    [Fact]
    public async Task GetBarDataAsync_Integration_ReturnsData()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var service = new BinanceExchangeService(httpClient);

        // Act
        var bars = await service.GetBarDataAsync(CoinPair.BTCUSDT, Interval.OneHour, CancellationToken.None);

        // Assert
        Assert.NotNull(bars);
        Assert.True(bars.Count > 0, "No bar data was returned from the service.");
    }

    // dotnet test --filter "FullyQualifiedName~BinanceExchangeServiceTests.ExecuteTradeAsync_Integration_ReturnsSuccess"
    [Fact]
    public async Task ExecuteTradeAsync_Integration_ReturnsSuccess()
    {
        // Arrange
        // Load API credentials from user secrets
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<BinanceExchangeServiceTests>()
            .Build();

        var apiKey = configuration["BinanceApi:ApiKey"];
        var apiSecret = configuration["BinanceApi:ApiSecret"];

        Assert.NotNull(apiKey);
        Assert.NotNull(apiSecret);

        Console.WriteLine("------ ExecuteTradeAsync Test ------");

        // Set up HttpClient with authentication
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);

        // Create service with API secret and use testnet
        var service = new BinanceExchangeService(httpClient, apiSecret, useTestnet: true);

        // Use a very small quantity for testing - Binance futures testnet requires min 0.001 BTC
        var symbol = "BTCUSDT";
        var quantity = 0.001;
        var tradeType = TradeType.Long;

        try
        {
            // Act
            Console.WriteLine($"Step 1: Executing {tradeType} trade for {quantity} {symbol}");
            var result = await service.ExecuteTradeAsync(symbol, quantity, tradeType, CancellationToken.None);

            // Assert
            Assert.True(result, "Trade execution failed");
            Console.WriteLine("✓ Trade executed successfully");
        }
        catch (Exception ex)
        {
            // Log the exception details and fail the test
            Console.WriteLine($"✗ Exception: {ex.Message}");
            throw;
        }
    }

    // dotnet test --filter "FullyQualifiedName~BinanceExchangeServiceTests.GetOpenOrdersAsync_Integration_ReturnsOpenOrders"
    [Fact]
    public async Task GetOpenOrdersAsync_Integration_ReturnsOpenOrders()
    {
        // Arrange
        // Load API credentials from user secrets
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<BinanceExchangeServiceTests>()
            .Build();

        var apiKey = configuration["BinanceApi:ApiKey"];
        var apiSecret = configuration["BinanceApi:ApiSecret"];

        Assert.NotNull(apiKey);
        Assert.NotNull(apiSecret);

        Console.WriteLine("------ GetOpenOrdersAsync Test ------");

        // Set up HttpClient with authentication
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);

        // Create service with API secret and use testnet
        var service = new BinanceExchangeService(httpClient, apiSecret, useTestnet: true);

        try
        {
            // Act
            var symbol = "BTCUSDT";
            Console.WriteLine($"Step 1: Retrieving open orders for {symbol}");
            var openOrders = await service.GetOpenOrdersAsync(symbol, CancellationToken.None);

            // Assert
            Assert.NotNull(openOrders);
            Console.WriteLine($"✓ Successfully retrieved {openOrders.Count} open orders");

            // Output summary of orders
            if (openOrders.Count > 0)
            {
                Console.WriteLine($"Order Summary:");
                foreach (var order in openOrders)
                {
                    Console.WriteLine($"  Order ID: {order.OrderId}, Type: {order.Type}, Side: {order.Side}, Status: {order.Status}");
                }
            }
        }
        catch (Exception ex)
        {
            // Log the exception details and fail the test
            Console.WriteLine($"✗ Exception: {ex.Message}");
            throw;
        }
    }

    // dotnet test --filter "FullyQualifiedName~BinanceExchangeServiceTests.GetPositionInformationAsync_Integration_ReturnsPositions"
    [Fact]
    public async Task GetPositionInformationAsync_Integration_ReturnsPositions()
    {
        // Arrange
        // Load API credentials from user secrets
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<BinanceExchangeServiceTests>()
            .Build();

        var apiKey = configuration["BinanceApi:ApiKey"];
        var apiSecret = configuration["BinanceApi:ApiSecret"];

        Assert.NotNull(apiKey);
        Assert.NotNull(apiSecret);

        Console.WriteLine("------ GetPositionInformationAsync Test ------");

        // Set up HttpClient with authentication
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);

        // Create service with API secret and use testnet
        var service = new BinanceExchangeService(httpClient, apiSecret, useTestnet: true);

        try
        {
            // Act - query positions for all symbols
            Console.WriteLine("Step 1: Retrieving all positions");
            var allPositionsResult = await service.GetPositionInformationAsync(null, CancellationToken.None);
            var positions = allPositionsResult as List<BinancePositionInfoResponse>;

            // Assert
            Assert.NotNull(positions);

            // Output summary of positions
            Console.WriteLine($"✓ Successfully retrieved {positions.Count} positions");
            var activePositions = positions.Count(p => decimal.Parse(p.PositionAmount) != 0);
            Console.WriteLine($"Active positions: {activePositions} of {positions.Count} total");

            if (activePositions > 0)
            {
                Console.WriteLine("Active Position Summary:");
                foreach (var position in positions.Where(p => decimal.Parse(p.PositionAmount) != 0))
                {
                    Console.WriteLine($"  Symbol: {position.Symbol}, Side: {position.PositionSide}, " +
                                    $"Amount: {position.PositionAmount}, PnL: {position.UnrealizedProfit}");
                }
            }

            // Create a new HttpClient for the second request
            using var httpClient2 = new HttpClient();
            httpClient2.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);
            var service2 = new BinanceExchangeService(httpClient2, apiSecret, useTestnet: true);

            // Query for a specific symbol with the new client
            var symbolToQuery = "BTCUSDT";
            Console.WriteLine($"\nStep 2: Retrieving positions for {symbolToQuery}");
            var btcPositionsResult = await service2.GetPositionInformationAsync(symbolToQuery, CancellationToken.None);
            var btcPositions = btcPositionsResult as List<BinancePositionInfoResponse>;

            Assert.NotNull(btcPositions);
            Console.WriteLine($"✓ Successfully retrieved {btcPositions.Count} positions for {symbolToQuery}");

            if (btcPositions.Count > 0)
            {
                Console.WriteLine($"{symbolToQuery} Position Summary:");
                foreach (var position in btcPositions)
                {
                    Console.WriteLine($"  Side: {position.PositionSide}, Amount: {position.PositionAmount}, " +
                                    $"Entry: {position.EntryPrice}, Mark: {position.MarkPrice}");
                }
            }
        }
        catch (Exception ex)
        {
            // Log the exception details and fail the test
            Console.WriteLine($"✗ Exception: {ex.Message}");
            throw;
        }
    }

    // dotnet test --filter "FullyQualifiedName~BinanceExchangeServiceTests.GetAccountBalanceAsync_Integration_ReturnsBalance"
    [Fact]
    public async Task GetAccountBalanceAsync_Integration_ReturnsBalance()
    {
        // Arrange
        // Load API credentials from user secrets
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<BinanceExchangeServiceTests>()
            .Build();

        var apiKey = configuration["BinanceApi:ApiKey"];
        var apiSecret = configuration["BinanceApi:ApiSecret"];

        Assert.NotNull(apiKey);
        Assert.NotNull(apiSecret);

        Console.WriteLine("------ GetAccountBalanceAsync Test ------");

        // Set up HttpClient with authentication
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);

        // Create service with API secret and use testnet
        var service = new BinanceExchangeService(httpClient, apiSecret, useTestnet: true);

        try
        {
            // Act
            Console.WriteLine("Step 1: Retrieving account balances");
            var balanceResult = await service.GetAccountBalanceAsync(CancellationToken.None);
            var balances = balanceResult as List<BinanceAccountBalanceResponse>;

            // Assert
            Assert.NotNull(balances);

            // Output summary of balances
            Console.WriteLine($"✓ Successfully retrieved {balances.Count} asset balances");

            // Show balances with non-zero available amounts
            var nonZeroBalances = balances.Where(b => decimal.Parse(b.AvailableBalance) > 0).ToList();
            Console.WriteLine($"Non-zero balances: {nonZeroBalances.Count} assets");

            if (nonZeroBalances.Count > 0)
            {
                Console.WriteLine("Non-zero Balance Summary:");
                foreach (var balance in nonZeroBalances)
                {
                    Console.WriteLine($"  Asset: {balance.Asset}, Available: {balance.AvailableBalance}");
                }
            }

            // Show USDT balance specifically (commonly used in futures)
            var usdtBalance = balances.FirstOrDefault(b => b.Asset == "USDT");
            if (usdtBalance != null)
            {
                Console.WriteLine("\nUSDT Balance Details:");
                Console.WriteLine($"  Available: {usdtBalance.AvailableBalance}");
                Console.WriteLine($"  Wallet: {usdtBalance.Balance}");
            }
        }
        catch (Exception ex)
        {
            // Log the exception details and fail the test
            Console.WriteLine($"✗ Exception: {ex.Message}");
            throw;
        }
    }

    // dotnet test --filter "FullyQualifiedName~BinanceExchangeServiceTests.QueryOrderAsync_Integration_ReturnsOrderInformation"
    [Fact]
    public async Task QueryOrderAsync_Integration_ReturnsOrderInformation()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<BinanceExchangeServiceTests>()
            .Build();

        var apiKey = configuration["BinanceApi:ApiKey"];
        var apiSecret = configuration["BinanceApi:ApiSecret"];

        Assert.NotNull(apiKey);
        Assert.NotNull(apiSecret);

        Console.WriteLine("------ QueryOrderAsync Test ------");

        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);

        var service = new BinanceExchangeService(httpClient, apiSecret, useTestnet: true);

        try
        {
            // First create a test order to query
            var symbol = "BTCUSDT";
            var quantity = 0.001;
            var tradeType = TradeType.Long;

            Console.WriteLine("Step 1: Creating a test order");
            var orderResult = await service.ExecuteTradeAsync(symbol, quantity, tradeType, CancellationToken.None);

            if (!orderResult)
            {
                Console.WriteLine("✗ Failed to create test order - skipping test");
                return;
            }

            // Wait a bit for the order to be processed
            await Task.Delay(1000);

            // Get open orders to find the order ID
            var openOrders = await service.GetOpenOrdersAsync(symbol, CancellationToken.None);

            if (openOrders.Count == 0)
            {
                // If no open orders, the test order may have been filled immediately
                Console.WriteLine("! No open orders found - order may have been filled immediately");
                return;
            }

            var orderToQuery = openOrders.First();
            Console.WriteLine($"Step 2: Querying order with ID: {orderToQuery.OrderId}");

            // Act
            var orderInfo = await service.QueryOrderAsync(symbol, orderToQuery.OrderId, CancellationToken.None);
            var orderResponse = orderInfo as BinanceOrderResponse;

            // Assert
            Assert.NotNull(orderResponse);
            Console.WriteLine($"✓ Successfully retrieved order details: ID={orderResponse.OrderId}, Status={orderResponse.Status}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Exception: {ex.Message}");
            throw;
        }
    }

    // dotnet test --filter "FullyQualifiedName~BinanceExchangeServiceTests.ChangeLeverageAsync_Integration_ChangesLeverage"
    [Fact]
    public async Task ChangeLeverageAsync_Integration_ChangesLeverage()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<BinanceExchangeServiceTests>()
            .Build();

        var apiKey = configuration["BinanceApi:ApiKey"];
        var apiSecret = configuration["BinanceApi:ApiSecret"];

        Assert.NotNull(apiKey);
        Assert.NotNull(apiSecret);

        Console.WriteLine("------ ChangeLeverageAsync Test ------");

        // Create a fresh HttpClient instance for each request to avoid reuse issues
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);

        var service = new BinanceExchangeService(httpClient, apiSecret, useTestnet: true);

        try
        {
            // Act
            var symbol = "BTCUSDT";
            var leverage = 5; // Change to 5x leverage
            Console.WriteLine($"Step 1: Changing leverage for {symbol} to {leverage}x");

            var result = await service.ChangeLeverageAsync(symbol, leverage, CancellationToken.None);

            // Assert
            Assert.True(result, "Failed to change leverage");
            Console.WriteLine($"✓ Successfully changed leverage to {leverage}x");

            // Try a different leverage to confirm it works
            using var httpClient2 = new HttpClient();
            httpClient2.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);
            var service2 = new BinanceExchangeService(httpClient2, apiSecret, useTestnet: true);

            // Change to a different leverage
            var leverage2 = 10;
            Console.WriteLine($"Step 2: Changing leverage for {symbol} to {leverage2}x");
            var result2 = await service2.ChangeLeverageAsync(symbol, leverage2, CancellationToken.None);

            Assert.True(result2, "Failed to change leverage to second value");
            Console.WriteLine($"✓ Successfully changed leverage to {leverage2}x");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Exception: {ex.Message}");
            Assert.Fail($"Test failed with exception: {ex.Message}");
        }
    }

    // dotnet test --filter "FullyQualifiedName~BinanceExchangeServiceTests.CancelOrderAsync_Integration_CancelsOrder"
    [Fact]
    public async Task CancelOrderAsync_Integration_CancelsOrder()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<BinanceExchangeServiceTests>()
            .Build();

        var apiKey = configuration["BinanceApi:ApiKey"];
        var apiSecret = configuration["BinanceApi:ApiSecret"];

        Assert.NotNull(apiKey);
        Assert.NotNull(apiSecret);

        Console.WriteLine("------ CancelOrderAsync Test ------");

        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);

        var service = new BinanceExchangeService(httpClient, apiSecret, useTestnet: true);

        try
        {
            // First create a test order to cancel
            var symbol = "BTCUSDT";
            var quantity = 0.001;
            var tradeType = TradeType.Long;

            Console.WriteLine("Step 1: Creating a test order to cancel");
            var orderResult = await service.ExecuteTradeAsync(symbol, quantity, tradeType, CancellationToken.None);

            if (!orderResult)
            {
                Console.WriteLine("✗ Failed to create test order - skipping test");
                return;
            }

            // Wait a bit for the order to be processed
            await Task.Delay(1000);

            // Get open orders to find the order ID
            var openOrders = await service.GetOpenOrdersAsync(symbol, CancellationToken.None);

            if (openOrders.Count == 0)
            {
                // If no open orders, the test order may have been filled immediately
                Console.WriteLine("! No open orders found - order may have been filled immediately");
                return;
            }

            var orderToCancel = openOrders.First();
            Console.WriteLine($"Step 2: Cancelling order with ID: {orderToCancel.OrderId}");

            // Act
            var cancelResult = await service.CancelOrderAsync(symbol, orderToCancel.OrderId, CancellationToken.None);

            // Assert
            Assert.True(cancelResult, "Failed to cancel order");
            Console.WriteLine($"✓ Successfully requested cancellation of order {orderToCancel.OrderId}");

            // Verify the order was cancelled
            await Task.Delay(1000); // Give some time for cancellation to process
            Console.WriteLine("Step 3: Verifying order was cancelled");
            var updatedOpenOrders = await service.GetOpenOrdersAsync(symbol, CancellationToken.None);
            var orderStillExists = updatedOpenOrders.Any(o => o.OrderId == orderToCancel.OrderId);

            Assert.False(orderStillExists, "Order was not cancelled successfully");
            Console.WriteLine("✓ Order cancellation confirmed - order no longer in open orders");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Exception: {ex.Message}");
            throw;
        }
    }

    // dotnet test --filter "FullyQualifiedName~BinanceExchangeServiceTests.CancelAllOrdersAsync_Integration_CancelsAllOrders"
    [Fact]
    public async Task CancelAllOrdersAsync_Integration_CancelsAllOrders()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<BinanceExchangeServiceTests>()
            .Build();

        var apiKey = configuration["BinanceApi:ApiKey"];
        var apiSecret = configuration["BinanceApi:ApiSecret"];

        Assert.NotNull(apiKey);
        Assert.NotNull(apiSecret);

        Console.WriteLine("------ CancelAllOrdersAsync Test ------");

        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", apiKey);

        var service = new BinanceExchangeService(httpClient, apiSecret, useTestnet: true);

        try
        {
            // First create a few test orders to cancel
            var symbol = "BTCUSDT";
            var quantity = 0.001;

            Console.WriteLine("Step 1: Creating test orders to cancel");

            // Create two test orders
            var order1Result = await service.ExecuteTradeAsync(symbol, quantity, TradeType.Long, CancellationToken.None);
            var order2Result = await service.ExecuteTradeAsync(symbol, quantity, TradeType.Long, CancellationToken.None);

            if (!order1Result && !order2Result)
            {
                Console.WriteLine("✗ Failed to create any test orders - skipping test");
                return;
            }

            // Wait a bit for the orders to be processed
            await Task.Delay(1000);

            // Get open orders to verify we have orders to cancel
            var openOrders = await service.GetOpenOrdersAsync(symbol, CancellationToken.None);
            var orderCount = openOrders.Count;
            Console.WriteLine($"Step 2: Found {orderCount} open orders to cancel");

            // Skip test if no orders exist
            if (orderCount == 0)
            {
                Console.WriteLine("! No open orders found - orders may have been filled immediately");
                return;
            }

            // Act
            Console.WriteLine("Step 3: Cancelling all open orders");
            var cancelResult = await service.CancelAllOrdersAsync(symbol, CancellationToken.None);

            // Assert
            Assert.True(cancelResult, "Failed to cancel all orders");
            Console.WriteLine("✓ Cancel all orders request successful");

            // Verify all orders were cancelled
            await Task.Delay(1000); // Give some time for cancellation to process
            Console.WriteLine("Step 4: Verifying all orders were cancelled");
            var updatedOpenOrders = await service.GetOpenOrdersAsync(symbol, CancellationToken.None);

            Assert.Empty(updatedOpenOrders);
            Console.WriteLine("✓ Order cancellation confirmed - no open orders remaining");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Exception: {ex.Message}");
            throw;
        }
    }
}