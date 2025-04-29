using System.Text.Json.Serialization;

namespace EzBot.Services.Response;

public class BinanceAccountBalanceResponse
{
    [JsonPropertyName("accountAlias")]
    public string AccountAlias { get; set; } = string.Empty;

    [JsonPropertyName("asset")]
    public string Asset { get; set; } = string.Empty;

    [JsonPropertyName("balance")]
    public string Balance { get; set; } = string.Empty;

    [JsonPropertyName("crossWalletBalance")]
    public string CrossWalletBalance { get; set; } = string.Empty;

    [JsonPropertyName("crossUnPnl")]
    public string CrossUnrealizedPnL { get; set; } = string.Empty;

    [JsonPropertyName("availableBalance")]
    public string AvailableBalance { get; set; } = string.Empty;

    [JsonPropertyName("maxWithdrawAmount")]
    public string MaxWithdrawAmount { get; set; } = string.Empty;

    [JsonPropertyName("marginAvailable")]
    public bool MarginAvailable { get; set; }

    [JsonPropertyName("updateTime")]
    public long UpdateTime { get; set; }
}