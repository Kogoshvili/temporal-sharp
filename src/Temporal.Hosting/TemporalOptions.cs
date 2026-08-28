using Kogoshvili.Temporal.Configuration;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Options for the Temporal worker starter, bound from the <c>Temporal</c>
/// configuration section. Inherits the shared connection options and adds
/// hosting-specific metrics and test-server configuration.
/// </summary>
public sealed class TemporalOptions : TemporalConnectionOptions
{
    /// <summary>Gets or sets metrics configuration.</summary>
    public TemporalMetricsOptions Metrics { get; set; } = new();

    /// <summary>Gets or sets tracing configuration.</summary>
    public TemporalTracingOptions Tracing { get; set; } = new();

    /// <summary>Gets or sets Core log-forwarding configuration.</summary>
    public TemporalLoggingOptions Logging { get; set; } = new();

    /// <summary>Gets or sets the test-server toggle.</summary>
    public TemporalTestServerOptions TestServer { get; set; } = new();

    /// <summary>Gets or sets the startup connection-wait configuration.</summary>
    public TemporalConnectionWaitOptions ConnectionWait { get; set; } = new();

    /// <summary>
    /// Gets or sets payload-codec configuration. The codecs built from this are
    /// composed into a shared <c>DataConverter</c> applied to the client and all
    /// workers.
    /// </summary>
    public TemporalDataConverterOptions DataConverter { get; set; } = new();

    /// <summary>
    /// Gets or sets per-queue worker configuration, keyed by task queue name and
    /// bound from <c>Temporal:Workers</c>. Applies to the worker registered for
    /// the matching queue via <c>AddTemporalWorker</c>.
    /// </summary>
    public Dictionary<string, TemporalWorkerConfigOptions>? Workers { get; set; }

    /// <summary>
    /// Gets or sets activity-options presets bound from
    /// <c>Temporal:ActivityOptions</c> and exposed to workflows via
    /// <see cref="ActivityOptionsRegistry"/>. A single preset maps to both
    /// regular and local activities.
    /// </summary>
    public TemporalActivityOptions? ActivityOptions { get; set; }

    /// <summary>
    /// Gets or sets workflow start/execution configuration bound from
    /// <c>Temporal:Workflows</c> (default + per-type presets and ID conventions),
    /// exposed to callers via <see cref="WorkflowOptionsRegistry"/>.
    /// </summary>
    public TemporalWorkflowOptions? Workflows { get; set; }

    /// <summary>
    /// Gets or sets workflow-level settings bound from
    /// <c>Temporal:WorkflowSettings</c> and read from inside workflows via
    /// <see cref="WorkflowSettings"/>.
    /// </summary>
    public TemporalWorkflowSettings? WorkflowSettings { get; set; }

    /// <summary>
    /// Gets or sets health-check configuration, bound from
    /// <c>Temporal:HealthChecks</c>. The check itself is registered opt-in via
    /// <c>AddTemporalHealthChecks()</c>.
    /// </summary>
    public TemporalHealthChecksOptions HealthChecks { get; set; } = new();

    /// <summary>
    /// Gets or sets schedule definitions keyed by schedule ID, bound from
    /// <c>Temporal:Schedules</c> and registered idempotently at startup (see
    /// <see cref="TemporalScheduleRegistrar"/>).
    /// </summary>
    public Dictionary<string, TemporalScheduleOptions>? Schedules { get; set; }
}
