namespace EzBot.Models;

public class TradeOrder
{
    public ActionType ActionType { get; set; }
    public double StopLoss { get; set; }

    public TradeOrder(ActionType actionType, double stopLoss)
    {
        ActionType = actionType;
        StopLoss = stopLoss;
    }

}
