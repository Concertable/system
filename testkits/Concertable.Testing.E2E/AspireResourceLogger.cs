using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Concertable.Testing.E2E;

public sealed class AspireResourceLogger : IAsyncDisposable
{
    private readonly CancellationTokenSource cts = new();
    private readonly Task task;

    public AspireResourceLogger(ResourceNotificationService notifications, ResourceLoggerService loggers, ILogger logger)
    {
        task = Task.Run(async () =>
        {
            var streamed = new HashSet<string>();
            try
            {
                await foreach (var e in notifications.WatchAsync(cts.Token))
                {
                    logger.AspireResourceStateChanged(e.Resource.Name, e.Snapshot.State?.Text ?? "unknown");
                    if (streamed.Add(e.Resource.Name))
                        _ = StreamResourceLogsAsync(loggers, e.Resource, logger);
                }
            }
            catch (OperationCanceledException) { }
        });
    }

    private async Task StreamResourceLogsAsync(ResourceLoggerService loggers, IResource resource, ILogger logger)
    {
        try
        {
            await foreach (var batch in loggers.WatchAsync(resource).WithCancellation(cts.Token))
                foreach (var line in batch)
                    logger.AspireResourceLog(resource.Name, Redact(line.Content));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.AspireResourceLog(resource.Name, $"[log-stream error] {ex.Message}");
        }
    }

    // Strip secret-shaped tokens (Stripe keys, webhook signing secrets) so they don't
    // land in the uploaded diagnostics artifact.
    private static readonly Regex SecretPattern =
        new(@"((?:sk|rk)_(?:test|live)_|whsec_)[A-Za-z0-9]+", RegexOptions.Compiled);

    private static string Redact(string content) => SecretPattern.Replace(content, "$1***");

    public async ValueTask DisposeAsync()
    {
        await cts.CancelAsync();
        await task;
        cts.Dispose();
    }
}
