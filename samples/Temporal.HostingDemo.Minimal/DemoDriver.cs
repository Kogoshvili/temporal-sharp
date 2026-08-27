using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kogoshvili.Temporal.HostingDemo.Minimal;

/// <summary>
/// Self-starts each demo workflow using nothing but the workflow type and its
/// argument — the task queue, workflow ID, and activity options all resolve
/// from the starter's defaults. Demonstrates the greeting, the
/// <see cref="HeartbeatingActivity"/> download, the local activity, and the
/// <see cref="Saga"/> compensation helper.
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
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        var greeting = await workflows.StartAsync<GreetingWorkflow, string>(
            workflow => workflow.RunAsync("world"));
        logger.LogInformation("Greeting result: {Greeting}", await greeting.GetResultAsync());

        var download = await workflows.StartAsync<DownloadWorkflow, string>(
            workflow => workflow.RunAsync(10));
        logger.LogInformation("Download: {Result}", await download.GetResultAsync());

        var local = await workflows.StartAsync<LocalActivityWorkflow, string>(
            workflow => workflow.RunAsync());
        logger.LogInformation("Local activity: {Result}", await local.GetResultAsync());

        var saga = await workflows.StartAsync<SagaWorkflow, string>(
            workflow => workflow.RunAsync($"order-{Guid.NewGuid():N}"));
        logger.LogInformation("Saga: {Result}", await saga.GetResultAsync());
    }
}
