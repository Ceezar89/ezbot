using System.Text.Json.Serialization;

namespace EzBot.Services.Response;

public class BinancePositionInfoResponse
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("positionAmt")]
    public string PositionAmount { get; set; } = string.Empty;

    [JsonPropertyName("entryPrice")]
    public string EntryPrice { get; set; } = string.Empty;

    [JsonPropertyName("markPrice")]
    public string MarkPrice { get; set; } = string.Empty;

    [JsonPropertyName("unRealizedProfit")]
    public string UnrealizedProfit { get; set; } = string.Empty;

    [JsonPropertyName("liquidationPrice")]
    public string LiquidationPrice { get; set; } = string.Empty;

    [JsonPropertyName("leverage")]
    public string Leverage { get; set; } = string.Empty;

    [JsonPropertyName("maxNotionalValue")]
    public string MaxNotionalValue { get; set; } = string.Empty;

    [JsonPropertyName("marginType")]
    public string MarginType { get; set; } = string.Empty;

    [JsonPropertyName("isolatedMargin")]
    public string IsolatedMargin { get; set; } = string.Empty;

    [JsonPropertyName("isAutoAddMargin")]
    public string IsAutoAddMargin { get; set; } = string.Empty;

    [JsonPropertyName("positionSide")]
    public string PositionSide { get; set; } = string.Empty;

    [JsonPropertyName("notional")]
    public string Notional { get; set; } = string.Empty;

    [JsonPropertyName("isolatedWallet")]
    public string IsolatedWallet { get; set; } = string.Empty;

    [JsonPropertyName("updateTime")]
    public long UpdateTime { get; set; }
}