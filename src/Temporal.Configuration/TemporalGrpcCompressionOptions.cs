namespace Kogoshvili.Temporal.Configuration;

/// <summary>
/// Transport-level gRPC compression for the Temporal connection. <see cref="Mode"/>
/// is one of <c>"gzip"</c> (the SDK default) or <c>"none"</c>. Set the containing
/// <c>Temporal:GrpcCompression</c> section to <c>null</c> (the default) to keep
/// the SDK default (gzip).
/// </summary>
public sealed class TemporalGrpcCompressionOptions
{
    /// <summary>Compression modes understood by the connection.</summary>
    public const string Gzip = "gzip";

    /// <summary>The <see cref="Mode"/> value that disables transport compression.</summary>
    public const string None = "none";

    /// <summary>Gets or sets the compression mode (<c>"gzip"</c> or <c>"none"</c>). Default is gzip.</summary>
    public string Mode { get; set; } = Gzip;
}
