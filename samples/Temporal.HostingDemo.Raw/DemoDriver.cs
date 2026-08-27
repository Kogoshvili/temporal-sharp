using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Temporalio.Client;

namespace Kogoshvili.Temporal.HostingDemo.Raw;

/// <summary>
/// Self-starts the two demo workflows shortly after startup so the host is
/// self-demonstrating: it proves the manually-registered worker is live and
/// prints both a greeting and the activity-lifetime probe.
/// </summary>
public sealed class DemoDriver : BackgroundService
{
    private readonly ITemporalClient client;
    private readonly ILogger<DemoDriver> logger;

    public DemoDriver(ITemporalClient client, ILogger<DemoDriver> logger)
    {
        this.client = client;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give the worker's pollers a moment to connect to the server.
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken).ConfigureAwait(false);

        // NOTE: the starter's IWorkflowOps facade (Temporal:Workflows) merges a
        // Default preset, a per-type ByType override, and an Id:Format template,
        // deriving the workflow type from the generic. The hand-rolled equivalent
        // is exactly what's written below — construct a WorkflowOptions inline
        // with the desired timeouts/retry, task queue, and ID.
        var greetingHandle = await client.StartWorkflowAsync(
            (GreetingWorkflow workflow) => workflow.RunAsync("raw"),
            new() { Id = $"raw-greeting-{Guid.NewGuid():N}", TaskQueue = "raw-queue" });

        logger.LogInformation("Greeting result: {Greeting}", await greetingHandle.GetResultAsync());

        var probeHandle = await client.StartWorkflowAsync(
            (LifetimeProbeWorkflow workflow) => workflow.RunAsync(),
            new() { Id = $"raw-probe-{Guid.NewGuid():N}", TaskQueue = "raw-queue" });

        logger.LogInformation("Lifetime probe:{NewLine}{Probe}", Environment.NewLine, await probeHandle.GetResultAsync());

        var claimCheckHandle = await client.StartWorkflowAsync(
            (ClaimCheckWorkflow workflow) => workflow.RunAsync(new string('x', 4096)),
            new() { Id = $"raw-claimcheck-{Guid.NewGuid():N}", TaskQueue = "raw-queue" });

        logger.LogInformation("Claim-check result: {Result}", await claimCheckHandle.GetResultAsync());
    }
}
