namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Options for the Temporal worker starter, bound from the <c>Temporal</c>
/// configuration section.
/// </summary>
public sealed class TemporalOptions
{
    /// <summary>The configuration section name used by the starter.</summary>
    public const string SectionName = "Temporal";

    /// <summary>Gets or sets the Temporal server <c>host:port</c> to connect to.</summary>
    public string TargetHost { get; set; } = "localhost:7233";

    /// <summary>Gets or sets the Temporal namespace to connect to.</summary>
    public string Namespace { get; set; } = "default";

    /// <summary>Gets or sets the API key to send on every call.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Gets or sets TLS connection options, or <c>null</c> for no TLS.</summary>
    public TemporalTlsOptions? Tls { get; set; }

    /// <summary>Gets or sets metrics configuration.</summary>
    public TemporalMetricsOptions Metrics { get; set; } = new();

    /// <summary>Gets or sets the test-server toggle.</summary>
    public TemporalTestServerOptions TestServer { get; set; } = new();
}
