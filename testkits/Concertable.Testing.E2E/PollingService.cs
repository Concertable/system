using Microsoft.Extensions.Logging;

namespace Concertable.Testing.E2E;

public sealed class PollingService : IPollingService
{
    private readonly ILogger<PollingService> logger;

    public PollingService(ILogger<PollingService> logger)
    {
        this.logger = logger;
    }

    public async Task UntilAsync(
        Func<Task<bool>> condition,
        TimeSpan? timeout = null,
        TimeSpan? interval = null)
    {
        timeout ??= TimeSpan.FromSeconds(10);
        interval ??= TimeSpan.FromMilliseconds(250);

        using var cts = new CancellationTokenSource(timeout.Value);

        while (!cts.IsCancellationRequested)
        {
            try
            {
                if (await condition()) return;
            }
            catch (Exception ex)
            {
                logger.ConditionCheckFailed(ex);
            }

            try
            {
                await Task.Delay(interval.Value, cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        throw new TimeoutException("Condition was not met within the timeout.");
    }

    public async Task<T> UntilAsync<T>(
        Func<Task<T>> action,
        Func<T, bool> condition,
        TimeSpan? timeout = null,
        TimeSpan? interval = null)
    {
        timeout ??= TimeSpan.FromSeconds(10);
        interval ??= TimeSpan.FromMilliseconds(250);

        using var cts = new CancellationTokenSource(timeout.Value);

        while (!cts.IsCancellationRequested)
        {
            try
            {
                var result = await action();

                if (result is not null && condition(result))
                    return result;
            }
            catch (Exception ex)
            {
                logger.PollingActionFailed(ex);
            }

            try
            {
                await Task.Delay(interval.Value, cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        throw new TimeoutException("Condition was not met within the timeout.");
    }
}
