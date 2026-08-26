namespace Kogoshvili.Temporal.Configuration;

/// <summary>
/// DNS load-balancing options for the Temporal connection, mirroring the SDK's
/// <c>DnsLoadBalancingOptions</c>. When set, DNS resolution is performed
/// periodically and connections are load balanced across resolved addresses.
/// Set the containing <c>Temporal:DnsLoadBalancing</c> section to <c>null</c>
/// (the default) to disable load balancing. Duration values are bound from
/// configuration as time-span strings.
/// </summary>
public sealed class TemporalDnsLoadBalancingOptions
{
    /// <summary>Gets or sets the interval between DNS refreshes. Default is 30s.</summary>
    public TimeSpan ResolutionInterval { get; set; } = TimeSpan.FromSeconds(30);
}
