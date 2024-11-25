using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EzBot.Core.TradingBot;

namespace EzBot.Services;

public class TradingBotService : IHostedService
{
    private readonly ITradingBot _tradingBot;
    private readonly ILogger<TradingBotService> _logger;

    public TradingBotService(ITradingBot tradingBot, ILogger<TradingBotService> logger)
    {
        _tradingBot = tradingBot;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("TradingBotService is starting.");
        return _tradingBot.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("TradingBotService is stopping.");
        return _tradingBot.StopAsync();
    }

    public bool IsBotRunning => _tradingBot.IsRunning;
}