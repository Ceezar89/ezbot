using System.Text.Json.Serialization;

namespace EzBot.Services.DTO;

public record BinanceKlineResponse(
    [property: JsonPropertyName("0")] long OpenTime,
    [property: JsonPropertyName("1")] string Open,
    [property: JsonPropertyName("2")] string High,
    [property: JsonPropertyName("3")] string Low,
    [property: JsonPropertyName("4")] string Close,
    [property: JsonPropertyName("5")] string Volume,
    [property: JsonPropertyName("6")] long CloseTime,
    [property: JsonPropertyName("7")] string QuoteVolume,
    [property: JsonPropertyName("8")] int NumberOfTrades,
    [property: JsonPropertyName("9")] string TakerBuyBaseVolume,
    [property: JsonPropertyName("10")] string TakerBuyQuoteVolume,
    [property: JsonPropertyName("11")] string Ignore
);
