using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Temporalio.Client;

namespace Kogoshvili.Temporal.ConsoleWorker;

/// <summary>
/// Starts a single workflow shortly after startup so the console worker is
/// self-demonstrating: it hosts a worker, then exercises it through the injected
/// <see cref="ITemporalClient"/> and logs the result.
/// </summary>
public sealed class WorkerDemoService : BackgroundService
{
    private readonly ITemporalClient client;
    private readonly ILogger<WorkerDemoService> logger;

    public WorkerDemoService(ITemporalClient client, ILogger<WorkerDemoService> logger)
    {
        this.client = client;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give the worker's pollers a moment to connect to the test server.
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken).ConfigureAwait(false);

        var handle = await client.StartWorkflowAsync(
            (GreetingWorkflow workflow) => workflow.RunAsync("console"),
            new() { Id = $"console-{Guid.NewGuid():N}", TaskQueue = "console-queue" });

        var greeting = await handle.GetResultAsync();
        logger.LogInformation("Workflow result: {Greeting} (workflow id {WorkflowId})", greeting, handle.Id);
    }
}
