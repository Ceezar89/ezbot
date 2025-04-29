using EzBot.Models;
using EzBot.Services.Response;

namespace EzBot.Services.Exchange;

public interface ICryptoExchangeService
{
    Task<List<BarData>> GetBarDataAsync(CoinPair symbol, Interval interval, CancellationToken cancellationToken);
    Task<bool> ExecuteTradeAsync(string symbol, double quantity, TradeType tradeType, CancellationToken cancellationToken);

    // Query order information
    Task<object?> QueryOrderAsync(string symbol, long orderId, CancellationToken cancellationToken);

    // Change leverage
    Task<bool> ChangeLeverageAsync(string symbol, int leverage, CancellationToken cancellationToken);

    // Change margin type
    Task<bool> ChangeMarginTypeAsync(string symbol, string marginType, CancellationToken cancellationToken);

    // Change position mode
    Task<bool> ChangePositionModeAsync(bool dualSidePosition, CancellationToken cancellationToken);

    // Cancel order
    Task<bool> CancelOrderAsync(string symbol, long orderId, CancellationToken cancellationToken);

    // Cancel all open orders
    Task<bool> CancelAllOrdersAsync(string symbol, CancellationToken cancellationToken);

    // Get position information
    Task<object?> GetPositionInformationAsync(string? symbol, CancellationToken cancellationToken);

    // Get account balance
    Task<object?> GetAccountBalanceAsync(CancellationToken cancellationToken);
}