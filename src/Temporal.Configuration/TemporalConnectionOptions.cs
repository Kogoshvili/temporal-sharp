namespace Kogoshvili.Temporal.Configuration;

/// <summary>
/// Connection options for a Temporal service. This is the shared, transport-level
/// subset of the hosting starter's <c>TemporalOptions</c>: it carries only what
/// is needed to reach and authenticate against a Temporal server, so the CLI and
/// the testing harness can reuse it without pulling in the hosting stack.
/// </summary>
public class TemporalConnectionOptions
{
    /// <summary>The configuration section name these options bind from.</summary>
    public const string SectionName = "Temporal";

    /// <summary>Gets or sets the Temporal server <c>host:port</c> to connect to.</summary>
    public string TargetHost { get; set; } = "localhost:7233";

    /// <summary>Gets or sets the Temporal namespace to connect to.</summary>
    public string Namespace { get; set; } = "default";

    /// <summary>Gets or sets the API key to send on every call.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Gets or sets TLS connection options, or <c>null</c> for no TLS.</summary>
    public TemporalTlsOptions? Tls { get; set; }
}
