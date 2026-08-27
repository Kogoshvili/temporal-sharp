using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kogoshvili.Temporal.HostingDemo.Minimal;

/// <summary>
/// Starts the greeting workflow via <see cref="IWorkflowOps"/> using nothing but
/// the workflow type and its argument — the task queue comes from
/// <c>Temporal:Workflows:Default:TaskQueue</c> and the workflow ID is generated
/// by the SDK.
/// </summary>
public sealed class GreetingDriver : BackgroundService
{
    private readonly IWorkflowOps workflows;
    private readonly ILogger<GreetingDriver> logger;

    public GreetingDriver(IWorkflowOps workflows, ILogger<GreetingDriver> logger)
    {
        this.workflows = workflows;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        var handle = await workflows.StartAsync<GreetingWorkflow, string>(
            workflow => workflow.RunAsync("world"));

        logger.LogInformation("Greeting result: {Greeting}", await handle.GetResultAsync());
    }
}
