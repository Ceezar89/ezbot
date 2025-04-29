using System.Text.Json.Serialization;

namespace EzBot.Services.Response;

public class MexcAccountResponse : MexcBaseResponse
{
    [JsonPropertyName("data")]
    public MexcAccountData? Data { get; set; }
}

public class MexcAccountData
{
    [JsonPropertyName("availableBalance")]
    public string AvailableBalance { get; set; } = string.Empty;

    [JsonPropertyName("bonus")]
    public string Bonus { get; set; } = string.Empty;

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("equity")]
    public string Equity { get; set; } = string.Empty;

    [JsonPropertyName("maxWithdrawAmount")]
    public string MaxWithdrawAmount { get; set; } = string.Empty;

    [JsonPropertyName("positionMargin")]
    public string PositionMargin { get; set; } = string.Empty;

    [JsonPropertyName("unrealizedProfit")]
    public string UnrealizedProfit { get; set; } = string.Empty;

    [JsonPropertyName("walletBalance")]
    public string WalletBalance { get; set; } = string.Empty;
}