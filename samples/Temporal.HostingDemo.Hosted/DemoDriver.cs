using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Temporalio.Client;

namespace Kogoshvili.Temporal.HostingDemo.Hosted;

/// <summary>
/// Self-starts the two demo workflows shortly after startup so the host is
/// self-demonstrating: it proves the auto-discovered worker is live and prints
/// both a greeting and the activity-lifetime probe.
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
        // Give the worker's pollers a moment to connect to the test server.
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken).ConfigureAwait(false);

        var greetingHandle = await client.StartWorkflowAsync(
            (GreetingWorkflow workflow) => workflow.RunAsync("hosted"),
            new() { Id = $"hosted-greeting-{Guid.NewGuid():N}", TaskQueue = "hosted-queue" });

        logger.LogInformation("Greeting result: {Greeting}", await greetingHandle.GetResultAsync());

        var probeHandle = await client.StartWorkflowAsync(
            (LifetimeProbeWorkflow workflow) => workflow.RunAsync(),
            new() { Id = $"hosted-probe-{Guid.NewGuid():N}", TaskQueue = "hosted-queue" });

        logger.LogInformation("Lifetime probe:{NewLine}{Probe}", Environment.NewLine, await probeHandle.GetResultAsync());
    }
}
