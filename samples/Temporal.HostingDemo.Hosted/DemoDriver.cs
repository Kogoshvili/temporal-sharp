using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Temporalio.Client;

namespace Kogoshvili.Temporal.HostingDemo.Hosted;

/// <summary>
/// Self-starts the demo workflows shortly after startup so the host is
/// self-demonstrating: it proves the auto-discovered worker is live and prints
/// the greeting, the activity-lifetime probe, and the claim-check round-trip.
/// </summary>
public sealed class DemoDriver : BackgroundService
{
    private readonly ITemporalClient client;
    private readonly WorkflowOptionsRegistry workflows;
    private readonly ILogger<DemoDriver> logger;

    public DemoDriver(ITemporalClient client, WorkflowOptionsRegistry workflows, ILogger<DemoDriver> logger)
    {
        this.client = client;
        this.workflows = workflows;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give the worker's pollers a moment to connect to the server.
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken).ConfigureAwait(false);

        // Start via WorkflowOptionsRegistry: Temporal:Workflows:Default +
        // Temporal:Workflows:ByType:GreetingWorkflow presets are merged, the
        // Temporal:Workflows:Id:Format convention supplies the workflow ID, and
        // the final configure delegate overrides everything.
        var greetingOptions = workflows.Build(
            "GreetingWorkflow",
            "hosted-queue",
            configure: o => o.TaskTimeout = TimeSpan.FromSeconds(30));

        var greetingHandle = await client.StartWorkflowAsync(
            (GreetingWorkflow workflow) => workflow.RunAsync("hosted"),
            greetingOptions);

        logger.LogInformation("Greeting result: {Greeting}", await greetingHandle.GetResultAsync());

        var probeHandle = await client.StartWorkflowAsync(
            (LifetimeProbeWorkflow workflow) => workflow.RunAsync(),
            new() { Id = $"hosted-probe-{Guid.NewGuid():N}", TaskQueue = "hosted-queue" });

        logger.LogInformation("Lifetime probe:{NewLine}{Probe}", Environment.NewLine, await probeHandle.GetResultAsync());

        // A payload big enough to trigger claim-check offloading.
        var claimCheckHandle = await client.StartWorkflowAsync(
            (ClaimCheckWorkflow workflow) => workflow.RunAsync(new string('x', 4096)),
            new() { Id = $"hosted-claimcheck-{Guid.NewGuid():N}", TaskQueue = "hosted-queue" });

        logger.LogInformation("Claim-check result: {Result}", await claimCheckHandle.GetResultAsync());
    }
}
