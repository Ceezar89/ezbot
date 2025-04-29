using System.Text.Json.Serialization;

namespace EzBot.Services.Response;

public class MexcPositionResponse : MexcBaseResponse
{
    [JsonPropertyName("data")]
    public List<MexcPositionData>? Data { get; set; }
}

public class MexcPositionData
{
    [JsonPropertyName("positionId")]
    public long PositionId { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("margin")]
    public string Margin { get; set; } = string.Empty;

    [JsonPropertyName("leverage")]
    public int Leverage { get; set; }

    [JsonPropertyName("side")]
    public string Side { get; set; } = string.Empty;

    [JsonPropertyName("availableVol")]
    public string AvailableVolume { get; set; } = string.Empty;

    [JsonPropertyName("avgPrice")]
    public string AveragePrice { get; set; } = string.Empty;

    [JsonPropertyName("openPrice")]
    public string OpenPrice { get; set; } = string.Empty;

    [JsonPropertyName("openMargin")]
    public string OpenMargin { get; set; } = string.Empty;

    [JsonPropertyName("closeVol")]
    public string CloseVolume { get; set; } = string.Empty;

    [JsonPropertyName("posMargin")]
    public string PositionMargin { get; set; } = string.Empty;

    [JsonPropertyName("posLoss")]
    public string PositionLoss { get; set; } = string.Empty;

    [JsonPropertyName("marketPrice")]
    public string MarketPrice { get; set; } = string.Empty;

    [JsonPropertyName("liquidationPrice")]
    public string LiquidationPrice { get; set; } = string.Empty;

    [JsonPropertyName("uTime")]
    public long UpdateTime { get; set; }
}