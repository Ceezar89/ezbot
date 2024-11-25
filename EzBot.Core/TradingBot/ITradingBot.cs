namespace EzBot.Core.TradingBot;

public interface ITradingBot
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync();
    bool IsRunning { get; }
}
