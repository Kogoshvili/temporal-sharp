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

    /// <summary>Gets or sets the test-server toggle.</summary>
    public TemporalTestServerOptions TestServer { get; set; } = new();
}
