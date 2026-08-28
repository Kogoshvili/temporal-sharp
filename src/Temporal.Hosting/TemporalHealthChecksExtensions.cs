using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Health-check registration for the Temporal starter. The check verifies the
/// shared client connection is serving and that every registered task queue has
/// an active poller (see <see cref="TemporalHealthCheck"/>). It can be disabled
/// at runtime via <c>Temporal:HealthChecks:Enabled</c>.
/// </summary>
public static class TemporalHealthChecksExtensions
{
    /// <summary>
    /// Registers the Temporal client/worker health check and returns the
    /// health-checks builder for further chaining.
    /// </summary>
    public static IHealthChecksBuilder AddTemporalHealthChecks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddHealthChecks()
            .AddCheck<TemporalHealthCheck>(
                "temporal",
                HealthStatus.Unhealthy,
                tags: new[] { "temporal" });
    }
}
