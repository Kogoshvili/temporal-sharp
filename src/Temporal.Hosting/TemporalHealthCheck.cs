using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Temporalio.Api.TaskQueue.V1;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Client;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Reports the liveness of the Temporal client and the registered workers:
/// the shared connection must be serving, and every registered task queue must
/// have at least one poller. Reports <see cref="HealthStatus.Degraded"/> when
/// the server is reachable but a queue has no pollers (worker not connected).
/// </summary>
public sealed class TemporalHealthCheck : IHealthCheck
{
    private readonly ITemporalClient client;
    private readonly IOptionsMonitor<TemporalOptions> options;
    private readonly TemporalWorkerTaskQueueRegistry registry;
    private readonly ILogger<TemporalHealthCheck> logger;

    public TemporalHealthCheck(
        ITemporalClient client,
        IOptionsMonitor<TemporalOptions> options,
        TemporalWorkerTaskQueueRegistry registry,
        ILogger<TemporalHealthCheck> logger)
    {
        this.client = client;
        this.options = options;
        this.registry = registry;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var temporal = options.CurrentValue;
        if (!temporal.HealthChecks.Enabled)
        {
            return HealthCheckResult.Healthy("Temporal health checks are disabled.");
        }

        var rpcOptions = new RpcOptions { CancellationToken = cancellationToken };

        var serving = await IsServingAsync(rpcOptions).ConfigureAwait(false);

        var pollers = new Dictionary<string, int?>(StringComparer.Ordinal);
        foreach (var queue in registry.TaskQueues)
        {
            pollers[queue] = await GetPollerCountAsync(queue, temporal.Namespace, rpcOptions).ConfigureAwait(false);
        }

        return Evaluate(serving, pollers);
    }

    /// <summary>
    /// Maps the collected liveness signals to a health result. Extracted for
    /// testability: a server that is not serving is unhealthy; a serving server
    /// with a queue that has no pollers (or could not be described) is degraded.
    /// </summary>
    internal static HealthCheckResult Evaluate(bool? serving, IReadOnlyDictionary<string, int?> pollers)
    {
        if (serving is not true)
        {
            return HealthCheckResult.Unhealthy("Temporal server is not reachable or not serving.");
        }

        var degradedQueues = new List<string>();
        var data = new Dictionary<string, object>();
        foreach (var (queue, count) in pollers)
        {
            if (count is null or 0)
            {
                degradedQueues.Add(queue);
            }

            data[$"{queue}:pollers"] = count is { } value ? value : "error";
        }

        return degradedQueues.Count > 0
            ? HealthCheckResult.Degraded(
                $"Temporal server is serving, but task queue(s) have no pollers: {string.Join(", ", degradedQueues)}.",
                data: data)
            : HealthCheckResult.Healthy("Temporal client and workers are healthy.", data);
    }

    private async Task<bool?> IsServingAsync(RpcOptions rpcOptions)
    {
        try
        {
            return await client.Connection.CheckHealthAsync(null, rpcOptions).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Temporal health check failed to reach the server.");
            return null;
        }
    }

    private async Task<int?> GetPollerCountAsync(string taskQueue, string ns, RpcOptions rpcOptions)
    {
        try
        {
            // ReportPollers is the only way to observe poller liveness in SDK
            // 1.18.0; the field is marked obsolete as part of the ENHANCED-mode
            // deprecation but remains the supported mechanism in DEFAULT mode.
#pragma warning disable CS0612
            var response = await client.Connection.WorkflowService.DescribeTaskQueueAsync(
                new DescribeTaskQueueRequest
                {
                    Namespace = ns,
                    TaskQueue = new TaskQueue { Name = taskQueue },
                    ReportPollers = true,
                },
                rpcOptions).ConfigureAwait(false);
#pragma warning restore CS0612

            return response.Pollers.Count;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Temporal health check failed to describe task queue {TaskQueue}.", taskQueue);
            return null;
        }
    }
}
