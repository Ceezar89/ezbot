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

public class LongTradeOrder(double stopLoss, double takeProfit) : TradeOrder(TradeType.Long, stopLoss, takeProfit)
{
}

public class ShortTradeOrder(double stopLoss, double takeProfit) : TradeOrder(TradeType.Short, stopLoss, takeProfit)
{
}