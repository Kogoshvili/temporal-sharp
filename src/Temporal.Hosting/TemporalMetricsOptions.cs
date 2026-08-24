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
