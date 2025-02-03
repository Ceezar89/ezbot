namespace EzBot.Models;

public class TradeOrder(TradeType tradeType, double stopLoss, double takeProfit = -1)
{
    public TradeType TradeType { get; set; } = tradeType;
    public double StopLoss { get; set; } = stopLoss;
    public double TakeProfit { get; set; } = takeProfit;
}
