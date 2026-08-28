namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Metrics configuration for the starter. When enabled, a
/// <see cref="System.Diagnostics.Metrics.Meter"/> is registered in the service
/// container and a client interceptor records workflow-start counts and
/// durations. The optional Prometheus/OpenTelemetry properties additionally
/// configure the SDK runtime to export its own metrics.
/// </summary>
public sealed class TemporalMetricsOptions
{
    /// <summary>Gets or sets a value indicating whether metrics are enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the name of the metrics meter.</summary>
    public string MeterName { get; set; } = "Temporal.Hosting";

    /// <summary>
    /// Gets or sets a value indicating whether the built-in metrics interceptor
    /// is wired into the client and workers. Set to <c>false</c> to record your
    /// own metrics via the SDK's <c>Interceptors</c> option instead.
    /// </summary>
    public bool UseDefaultInterceptor { get; set; } = true;

    /// <summary>
    /// Gets or sets the OpenTelemetry baggage keys whose values are attached as
    /// <c>baggage.&lt;key&gt;</c> tags on the recorded metrics. Empty by default;
    /// an explicit allowlist avoids leaking arbitrary baggage into metric tags.
    /// On the worker side these tags only appear when tracing is also enabled,
    /// since baggage is propagated and restored by the tracing interceptor.
    /// </summary>
    public string[] BaggageTagKeys { get; set; } = [];

    /// <summary>
    /// Gets or sets the address the SDK runtime exposes Prometheus metrics on
    /// (e.g. <c>0.0.0.0:9000</c>), or <c>null</c> to disable Prometheus export.
    /// </summary>
    public string? PrometheusBindAddress { get; set; }

    /// <summary>
    /// Gets or sets the OpenTelemetry collector URL the SDK runtime forwards
    /// metrics to (e.g. <c>http://localhost:4317</c>), or <c>null</c> to disable
    /// OpenTelemetry export.
    /// </summary>
    public string? OpenTelemetryUrl { get; set; }
}
