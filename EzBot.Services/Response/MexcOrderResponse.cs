using System.Text.Json.Serialization;

namespace EzBot.Services.Response;

public class MexcOrderResponse : MexcBaseResponse
{
    [JsonPropertyName("data")]
    public MexcOrderData? Data { get; set; }
}

public class MexcOrderData
{
    [JsonPropertyName("orderId")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("positionId")]
    public long PositionId { get; set; }

    [JsonPropertyName("price")]
    public string Price { get; set; } = string.Empty;

    [JsonPropertyName("vol")]
    public string Volume { get; set; } = string.Empty;

    [JsonPropertyName("leverage")]
    public int Leverage { get; set; }

    [JsonPropertyName("side")]
    public string Side { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("orderType")]
    public string OrderType { get; set; } = string.Empty;

    [JsonPropertyName("filledPrice")]
    public string FilledPrice { get; set; } = string.Empty;

    [JsonPropertyName("filledVol")]
    public string FilledVolume { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("createTime")]
    public long CreateTime { get; set; }

    [JsonPropertyName("updateTime")]
    public long UpdateTime { get; set; }
}