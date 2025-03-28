namespace EzBot.Models;

public enum BarValueType
{
    Open,
    High,
    Low,
    Close,
    Volume,
    HL2,    // (High + Low) / 2
    HLC3,   // (High + Low + Close) / 3
    OHLC4   // (Open + High + Low + Close) / 4
}