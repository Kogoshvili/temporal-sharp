using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Client.Schedules;
using Temporalio.Exceptions;

namespace Kogoshvili.Temporal.HostingDemo.Raw;

/// <summary>
/// The hand-rolled equivalent of the starter's idempotent schedule registration
/// (<c>TemporalScheduleRegistrar</c> + <c>Temporal:Schedules</c>). The SDK has no
/// "get-or-create": <c>CreateScheduleAsync</c> throws
/// <c>ScheduleAlreadyRunningException</c> when the ID exists, so we create and
/// swallow that exception to make registration safe to run on every startup.
/// The starter wraps exactly this in <c>IScheduleOps.RegisterAsync</c>.
/// </summary>
public sealed class RawScheduleRegistrar : IHostedService
{
    private readonly ITemporalClient client;
    private readonly ILogger<RawScheduleRegistrar> logger;

    public RawScheduleRegistrar(ITemporalClient client, ILogger<RawScheduleRegistrar> logger)
    {
        this.client = client;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var schedule = new Schedule(
            new ScheduleActionStartWorkflow(
                "LifetimeProbeWorkflow",
                Array.Empty<object?>(),
                new WorkflowOptions(id: "scheduled-lifetime-probe", taskQueue: "raw-queue")),
            new ScheduleSpec { CronExpressions = new[] { "0 0 * * *" } });

        try
        {
            await client.CreateScheduleAsync("daily-lifetime-probe", schedule);
            logger.LogInformation("Created schedule 'daily-lifetime-probe'.");
        }
        catch (ScheduleAlreadyRunningException)
        {
            logger.LogInformation("Schedule 'daily-lifetime-probe' already exists; leaving it as-is.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
