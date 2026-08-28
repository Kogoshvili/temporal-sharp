using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kogoshvili.Temporal.HostingDemo.Configured;

/// <summary>
/// Self-starts the demo workflows shortly after startup so the host is
/// self-demonstrating: it proves the auto-discovered worker is live and prints
/// the greeting, the activity-lifetime probe, and the claim-check round-trip.
/// </summary>
public sealed class DemoDriver : BackgroundService
{
    private readonly IWorkflowOps workflows;
    private readonly ILogger<DemoDriver> logger;

    public DemoDriver(IWorkflowOps workflows, ILogger<DemoDriver> logger)
    {
        this.workflows = workflows;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give the worker's pollers a moment to connect to the server.
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken).ConfigureAwait(false);

        // Minimal start via IWorkflowOps: workflow type comes from the generic,
        // the task queue from Temporal:Workflows:Default:TaskQueue, the workflow
        // ID from Temporal:Workflows:Id:Format, and timeouts from the
        // Default/ByType presets. An explicit per-call argument overrides any of
        // them (see the claim-check start below).
        var greetingHandle = await workflows.StartAsync<GreetingWorkflow, string, string>("hosted");

        logger.LogInformation("Greeting result: {Greeting}", await greetingHandle.GetResultAsync());

        var probeHandle = await workflows.StartAsync<LifetimeProbeWorkflow, string>();

        logger.LogInformation("Lifetime probe:{NewLine}{Probe}", Environment.NewLine, await probeHandle.GetResultAsync());

        // Override the task queue and workflow ID per-call (both always win).
        var claimCheckHandle = await workflows.StartAsync<ClaimCheckWorkflow, string, string>(
            new string('x', 4096),
            taskQueue: "configured-queue",
            workflowId: $"configured-claimcheck-{Guid.NewGuid():N}");

        logger.LogInformation("Claim-check result: {Result}", await claimCheckHandle.GetResultAsync());

        // Reads its own settings from Temporal:WorkflowSettings via a local activity.
        var batchingHandle = await workflows.StartAsync<BatchingWorkflow, string>();

        logger.LogInformation("Workflow settings: {Result}", await batchingHandle.GetResultAsync());

        // Saga: the Charge step fails, triggering LIFO compensation via the Saga helper.
        var sagaHandle = await workflows.StartAsync<SagaWorkflow, string, string>($"order-{Guid.NewGuid():N}");

        logger.LogInformation("Saga: {Result}", await sagaHandle.GetResultAsync());

        var localHandle = await workflows.StartAsync<LocalActivityWorkflow, string>();

        logger.LogInformation("Local activity: {Result}", await localHandle.GetResultAsync());

        // HeartbeatingActivity: download with auto-heartbeat + progress resume.
        var downloadHandle = await workflows.StartAsync<DownloadWorkflow, int, string>(10);

        logger.LogInformation("Download: {Result}", await downloadHandle.GetResultAsync());

        // ChildWorkflowOps: the parent starts a child whose options + ID resolve
        // from Temporal:Workflows (ByType + ChildFormat).
        var parentHandle = await workflows.StartAsync<ParentWorkflow, string, string>("child");

        logger.LogInformation("Parent/child: {Result}", await parentHandle.GetResultAsync());
    }
}
