using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Temporalio.Client.Schedules;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Registers schedules at startup, idempotently. Covers both config-driven
/// schedules (<c>Temporal:Schedules</c>) and code-driven schedules declared via
/// <see cref="TemporalScheduleRegistrationExtensions.AddTemporalSchedule(TemporalBuilder, string, Schedule, ScheduleOptions?, bool)"/>.
/// Each schedule is created if absent; a schedule declaring <c>reconcile: true</c>
/// is additionally driven toward the declared definition when it already exists.
/// Runs after the connection waiter so the server is reachable before
/// registration.
/// </summary>
public sealed class TemporalScheduleRegistrar : IHostedService
{
    private readonly IOptionsMonitor<TemporalOptions> options;
    private readonly IScheduleOps scheduleOps;
    private readonly IEnumerable<TemporalScheduleRegistration> registrations;
    private readonly ILogger<TemporalScheduleRegistrar> logger;

    /// <summary>Initializes a new instance of the <see cref="TemporalScheduleRegistrar"/> class.</summary>
    public TemporalScheduleRegistrar(
        IOptionsMonitor<TemporalOptions> options,
        IScheduleOps scheduleOps,
        IEnumerable<TemporalScheduleRegistration> registrations,
        ILogger<TemporalScheduleRegistrar> logger)
    {
        this.options = options;
        this.scheduleOps = scheduleOps;
        this.registrations = registrations;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var schedules = options.CurrentValue.Schedules;

        if (schedules is not null)
        {
            foreach (var (scheduleId, config) in schedules)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var schedule = ScheduleFactory.BuildSchedule(config, scheduleId);
                var createOptions = ScheduleFactory.BuildScheduleOptions(config);

                await scheduleOps.RegisterAsync(scheduleId, schedule, createOptions, config.Reconcile)
                    .ConfigureAwait(false);

                logger.LogInformation(
                    "Registered schedule '{ScheduleId}'{Reconcile}.",
                    scheduleId,
                    config.Reconcile ? " (reconciled)" : string.Empty);
            }
        }

        foreach (var registration in registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await scheduleOps.RegisterAsync(
                    registration.ScheduleId,
                    registration.Schedule,
                    registration.Options,
                    registration.Reconcile)
                .ConfigureAwait(false);

            logger.LogInformation(
                "Registered schedule '{ScheduleId}'{Reconcile}.",
                registration.ScheduleId,
                registration.Reconcile ? " (reconciled)" : string.Empty);
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
