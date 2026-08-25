using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Temporalio.Client;

namespace Kogoshvili.Temporal.HostingDemo.Raw;

/// <summary>
/// The raw equivalent of the starter's <c>TemporalConnectionWaiter</c>: connect
/// the shared lazy client (so the workers that follow reuse the open connection)
/// and retry with exponential backoff until the server is reachable.
/// </summary>
public sealed class RawConnectionWaiter : BackgroundService
{
    private readonly ITemporalClient client;
    private readonly ILogger<RawConnectionWaiter> logger;

    public RawConnectionWaiter(ITemporalClient client, ILogger<RawConnectionWaiter> logger)
    {
        this.client = client;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(1);
        while (true)
        {
            stoppingToken.ThrowIfCancellationRequested();

            try
            {
                await client.Connection.ConnectAsync().ConfigureAwait(false);
                logger.LogInformation("Connected to Temporal server.");
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Temporal server not reachable; retrying in {Delay}.", delay);
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);

                var maxDelay = TimeSpan.FromSeconds(15);
                delay = delay >= maxDelay ? maxDelay : delay * 2;
            }
        }
    }
}
