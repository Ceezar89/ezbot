using System.Text.Json.Serialization;

namespace EzBot.Services.Response;

public class MexcBaseResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}