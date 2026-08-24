using Microsoft.Extensions.DependencyInjection;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Builder returned by <c>AddTemporal</c> for chaining worker registration.
/// </summary>
public sealed class TemporalBuilder
{
    internal TemporalBuilder(IServiceCollection services) => Services = services;

    /// <summary>Gets the service collection being configured.</summary>
    public IServiceCollection Services { get; }
}
