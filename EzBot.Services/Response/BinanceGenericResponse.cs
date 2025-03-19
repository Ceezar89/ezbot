using System.Text.Json.Serialization;

namespace EzBot.Services.Response;

public class BinanceGenericResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string Message { get; set; } = string.Empty;
}

public class BinanceLeverageResponse : BinanceGenericResponse
{
    [JsonPropertyName("leverage")]
    public int Leverage { get; set; }

    [JsonPropertyName("maxNotionalValue")]
    public string MaxNotionalValue { get; set; } = string.Empty;

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;
}

public class BinanceMarginTypeResponse : BinanceGenericResponse
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("marginType")]
    public string MarginType { get; set; } = string.Empty;
}

public class BinancePositionModeResponse : BinanceGenericResponse
{
    [JsonPropertyName("dualSidePosition")]
    public bool DualSidePosition { get; set; }
}