using Microsoft.Extensions.Logging;

namespace EzBot.Core.TradingBot;

public class TradingBot : ITradingBot
{
    private readonly ILogger<TradingBot> _logger;
    private CancellationTokenSource _cts;
    private Task _executionTask;

    public bool IsRunning { get; private set; }

    public TradingBot(ILogger<TradingBot> logger)
    {
        _cts = null!;
        _executionTask = null!;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (IsRunning)
            return Task.CompletedTask;

        IsRunning = true;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _executionTask = Task.Run(async () =>
        {
            _logger.LogInformation("Trading bot started.");

            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    // Insert trading logic here
                    _logger.LogInformation("Trading bot is running...");
                    await Task.Delay(TimeSpan.FromSeconds(1), _cts.Token);
                }
            }
            catch (TaskCanceledException)
            {
                // Expected when task is canceled
            }
            finally
            {
                IsRunning = false;
                _logger.LogInformation("Trading bot stopped.");
            }
        }, _cts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (!IsRunning)
            return;

        _cts.Cancel();

        try
        {
            await _executionTask;
        }
        catch (TaskCanceledException)
        {
            // Expected when task is canceled
        }
        finally
        {
            IsRunning = false;
            _cts.Dispose();
        }
    }
}
