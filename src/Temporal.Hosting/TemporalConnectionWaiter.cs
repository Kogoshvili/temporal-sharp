using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Temporalio.Client;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Hosted service that waits for the Temporal server to be reachable before
/// workers start polling. It connects the shared lazy <see cref="ITemporalClient"/>
/// (a no-op for workers that follow, which reuse the now-open connection) and
/// retries with exponential backoff until success or
/// <see cref="TemporalConnectionWaitOptions.Timeout"/> elapses. Skipped when the
/// test server is enabled, which starts its own in-process server.
/// </summary>
public sealed class TemporalConnectionWaiter : IHostedService
{
    private readonly IOptionsMonitor<TemporalOptions> options;
    private readonly ITemporalClient client;
    private readonly ILogger<TemporalConnectionWaiter> logger;

    public TemporalConnectionWaiter(
        IOptionsMonitor<TemporalOptions> options,
        ITemporalClient client,
        ILogger<TemporalConnectionWaiter> logger)
    {
        this.options = options;
        this.client = client;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var current = options.CurrentValue;
        if (!current.ConnectionWait.Enabled || current.TestServer.Enabled)
        {
            return;
        }

        var wait = current.ConnectionWait;
        var deadline = wait.Timeout is { } timeout ? DateTime.UtcNow + timeout : (DateTime?)null;
        var delay = wait.InitialDelay;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await client.Connection.ConnectAsync().ConfigureAwait(false);
                logger.LogInformation("Connected to Temporal server at {TargetHost}.", current.TargetHost);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (deadline is { } d && DateTime.UtcNow >= d)
                {
                    logger.LogError(
                        ex,
                        "Timed out waiting for Temporal server at {TargetHost}.",
                        current.TargetHost);
                    throw;
                }

                logger.LogWarning(
                    ex,
                    "Temporal server not reachable at {TargetHost}; retrying in {Delay}.",
                    current.TargetHost,
                    delay);

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

                delay = NextDelay(delay, wait.MaxDelay);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static TimeSpan NextDelay(TimeSpan delay, TimeSpan maxDelay)
    {
        var doubled = delay.Ticks >= long.MaxValue / 2
            ? TimeSpan.MaxValue
            : TimeSpan.FromTicks(delay.Ticks * 2);

        return doubled < maxDelay ? doubled : maxDelay;
    }
}
