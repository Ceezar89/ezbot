using System.Text.Json.Serialization;

namespace EzBot.Services.Response;

public class MexcKlineResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("data")]
    public required MexcKlineData Data { get; set; }
}

public class MexcKlineData
{
    [JsonPropertyName("time")]
    public required long[] Time { get; set; }

    [JsonPropertyName("open")]
    public required double[] Open { get; set; }

    [JsonPropertyName("close")]
    public required double[] Close { get; set; }

    [JsonPropertyName("high")]
    public required double[] High { get; set; }

    [JsonPropertyName("low")]
    public required double[] Low { get; set; }

    [JsonPropertyName("vol")]
    public required double[] Vol { get; set; }

    [JsonPropertyName("amount")]
    public required double[] Amount { get; set; }

    [JsonPropertyName("realOpen")]
    public required double[] RealOpen { get; set; }

    [JsonPropertyName("realClose")]
    public required double[] RealClose { get; set; }

    [JsonPropertyName("realHigh")]
    public required double[] RealHigh { get; set; }

    [JsonPropertyName("realLow")]
    public required double[] RealLow { get; set; }
}