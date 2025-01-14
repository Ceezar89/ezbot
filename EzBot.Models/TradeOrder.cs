namespace EzBot.Models;

public class TradeOrder(TradeType tradeType, double stopLoss)
{
    public TradeType TradeType { get; set; } = tradeType;
    public double StopLoss { get; set; } = stopLoss;
}
