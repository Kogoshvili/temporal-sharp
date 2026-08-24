namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Metrics configuration for the starter. When enabled, a
/// <see cref="System.Diagnostics.Metrics.Meter"/> is registered in the service
/// container and a client interceptor records workflow-start counts and
/// durations.
/// </summary>
public sealed class TemporalMetricsOptions
{
    /// <summary>Gets or sets a value indicating whether metrics are enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the name of the metrics meter.</summary>
    public string MeterName { get; set; } = "Temporal.Hosting";
}
