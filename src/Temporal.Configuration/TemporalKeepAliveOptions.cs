namespace Kogoshvili.Temporal.Configuration;

/// <summary>
/// HTTP/2 keep-alive options for the Temporal connection, mirroring the SDK's
/// <c>KeepAliveOptions</c>. Duration values are bound from configuration as
/// time-span strings (e.g. <c>"00:00:30"</c>). Set the containing
/// <c>Temporal:KeepAlive</c> section to <c>null</c> (the default) to keep the
/// SDK defaults.
/// </summary>
public sealed class TemporalKeepAliveOptions
{
    /// <summary>Gets or sets the interval between keep-alive pings. Default is 30s.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the timeout a ping must be answered within. Default is 15s.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);
}
