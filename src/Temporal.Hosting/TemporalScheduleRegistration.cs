namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// A programmatically-declared schedule to be registered at startup by
/// <see cref="TemporalScheduleRegistrar"/>, added via
/// <see cref="TemporalScheduleRegistrationExtensions.AddTemporalSchedule(TemporalBuilder, string, Temporalio.Client.Schedules.Schedule, Temporalio.Client.Schedules.ScheduleOptions?, bool)"/>.
/// Unlike the config-driven <c>Temporal:Schedules</c> entries, this carries a
/// fully-built <see cref="Temporalio.Client.Schedules.Schedule"/> and therefore
/// supports typed workflow arguments.
/// </summary>
public sealed record TemporalScheduleRegistration(
    string ScheduleId,
    Temporalio.Client.Schedules.Schedule Schedule,
    Temporalio.Client.Schedules.ScheduleOptions? Options,
    bool Reconcile);
