using System.Text.Json.Serialization;

namespace EzBot.Services.Response;

// Using the existing BinanceOrderResponse model for open orders
// as the structure is the same for both endpoints
public class BinanceOpenOrdersResponse : BinanceOrderResponse
{
    // No additional properties needed
}