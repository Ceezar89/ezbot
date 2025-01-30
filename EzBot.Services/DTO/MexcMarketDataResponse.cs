
using System.Text.Json.Serialization;

namespace EzBot.Services.DTO;

public record MexcKlineResponse(
    [property: JsonPropertyName("t")] long Time,
    [property: JsonPropertyName("o")] string Open,
    [property: JsonPropertyName("h")] string High,
    [property: JsonPropertyName("l")] string Low,
    [property: JsonPropertyName("c")] string Close,
    [property: JsonPropertyName("v")] string Volume,
    [property: JsonPropertyName("a")] string Amount
);