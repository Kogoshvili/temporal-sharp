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

    /// <summary>
    /// Gets or sets the RPC retry policy applied to the connection, or
    /// <c>null</c> to leave the SDK defaults unchanged.
    /// </summary>
    public TemporalRpcRetryOptions? RpcRetry { get; set; }

    /// <summary>
    /// Gets or sets HTTP/2 keep-alive options, or <c>null</c> to leave the SDK
    /// defaults unchanged.
    /// </summary>
    public TemporalKeepAliveOptions? KeepAlive { get; set; }

    /// <summary>
    /// Gets or sets HTTP CONNECT proxy options, or <c>null</c> to connect directly.
    /// </summary>
    public TemporalHttpConnectProxyOptions? HttpConnectProxy { get; set; }

    /// <summary>
    /// Gets or sets DNS load-balancing options, or <c>null</c> to disable load
    /// balancing.
    /// </summary>
    public TemporalDnsLoadBalancingOptions? DnsLoadBalancing { get; set; }

    /// <summary>
    /// Gets or sets transport-level gRPC compression options, or <c>null</c> to
    /// keep the SDK default (gzip).
    /// </summary>
    public TemporalGrpcCompressionOptions? GrpcCompression { get; set; }
}
