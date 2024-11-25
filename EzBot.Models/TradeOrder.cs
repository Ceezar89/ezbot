namespace EzBot.Models;

public class TradeOrder
{
    public TradeType TradeType { get; set; }
    public double StopLoss { get; set; }

    public TradeOrder(TradeType tradeType, double stopLoss)
    {
        TradeType = tradeType;
        StopLoss = stopLoss;
    }

}
