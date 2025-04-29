namespace EzBot.Models;

public class TradeOrder(double stopLoss, double takeProfit)
{
    public TradeType TradeType { get; set; } = TradeType.None;
    public double StopLoss { get; set; } = stopLoss;
    public double TakeProfit { get; set; } = takeProfit;

    public TradeOrder(TradeType tradeType, double stopLoss, double takeProfit) : this(stopLoss, takeProfit)
    {
        TradeType = tradeType;
    }
}