namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Health-check configuration, bound from <c>Temporal:HealthChecks</c>. The
/// health checks are registered opt-in via
/// <c>AddTemporalHealthChecks()</c>; <see cref="Enabled"/> additionally lets
/// the check be switched off at runtime without removing the registration.
/// </summary>
public sealed class TemporalHealthChecksOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the Temporal health check runs.
    /// When <c>false</c>, the check reports healthy without contacting the
    /// server. Default is <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
