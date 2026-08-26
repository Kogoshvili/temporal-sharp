namespace Kogoshvili.Temporal.Configuration;

/// <summary>
/// HTTP CONNECT proxy options for the Temporal connection, mirroring the SDK's
/// <c>HttpConnectProxyOptions</c>. Username and password are flattened into
/// separate properties (the SDK exposes a <c>(string, string)?</c> tuple, which
/// does not bind cleanly from configuration). Set the containing
/// <c>Temporal:HttpConnectProxy</c> section to <c>null</c> (the default) to
/// connect directly.
/// </summary>
public sealed class TemporalHttpConnectProxyOptions
{
    /// <summary>Gets or sets the <c>host:port</c> of the proxy to route through.</summary>
    public string? TargetHost { get; set; }

    /// <summary>Gets or sets the proxy basic-auth username, or <c>null</c> for none.</summary>
    public string? Username { get; set; }

    /// <summary>Gets or sets the proxy basic-auth password, or <c>null</c> for none.</summary>
    public string? Password { get; set; }
}
