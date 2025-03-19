using System.Text.Json.Serialization;

namespace EzBot.Services.Response;

public class BinanceOrderResponse
{
    [JsonPropertyName("clientOrderId")]
    public string ClientOrderId { get; set; } = string.Empty;

    [JsonPropertyName("cumQty")]
    public string CumQty { get; set; } = string.Empty;

    [JsonPropertyName("cumQuote")]
    public string CumQuote { get; set; } = string.Empty;

    [JsonPropertyName("executedQty")]
    public string ExecutedQty { get; set; } = string.Empty;

    [JsonPropertyName("orderId")]
    public long OrderId { get; set; }

    [JsonPropertyName("avgPrice")]
    public string AvgPrice { get; set; } = string.Empty;

    [JsonPropertyName("origQty")]
    public string OrigQty { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public string Price { get; set; } = string.Empty;

    [JsonPropertyName("reduceOnly")]
    public bool ReduceOnly { get; set; }

    [JsonPropertyName("side")]
    public string Side { get; set; } = string.Empty;

    [JsonPropertyName("positionSide")]
    public string PositionSide { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("stopPrice")]
    public string StopPrice { get; set; } = string.Empty;

    [JsonPropertyName("closePosition")]
    public bool ClosePosition { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("timeInForce")]
    public string TimeInForce { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("origType")]
    public string OrigType { get; set; } = string.Empty;

    [JsonPropertyName("activatePrice")]
    public string? ActivatePrice { get; set; }

    [JsonPropertyName("priceRate")]
    public string? PriceRate { get; set; }

    [JsonPropertyName("updateTime")]
    public long UpdateTime { get; set; }

    [JsonPropertyName("workingType")]
    public string WorkingType { get; set; } = string.Empty;

    [JsonPropertyName("priceProtect")]
    public bool PriceProtect { get; set; }

    [JsonPropertyName("priceMatch")]
    public string PriceMatch { get; set; } = string.Empty;

    [JsonPropertyName("selfTradePreventionMode")]
    public string SelfTradePreventionMode { get; set; } = string.Empty;

    [JsonPropertyName("goodTillDate")]
    public long? GoodTillDate { get; set; }
}
