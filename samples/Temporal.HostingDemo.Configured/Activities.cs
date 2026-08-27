using Kogoshvili.Temporal.Hosting;
using Temporalio.Activities;

namespace Kogoshvili.Temporal.HostingDemo.Configured;

// Four activity classes that exercise every lifetime auto-discovery assigns:
// scoped (default for instance classes), singleton/transient (via
// [ActivityLifetime]), and static (default for static classes). Each method has
// a unique name, since the Temporal worker keys activities by name.

public sealed class ScopedActivities
{
    private readonly Guid instanceId = Guid.NewGuid();

    [Activity]
    public Task<string> ScopedProbe() => Task.FromResult(instanceId.ToString("N")[..8]);
}

[ActivityLifetime(ActivityLifetime.Singleton)]
public sealed class SingletonActivities
{
    private readonly Guid instanceId = Guid.NewGuid();

    [Activity]
    public Task<string> SingletonProbe() => Task.FromResult(instanceId.ToString("N")[..8]);
}

[ActivityLifetime(ActivityLifetime.Transient)]
public sealed class TransientActivities
{
    private readonly Guid instanceId = Guid.NewGuid();

    [Activity]
    public Task<string> TransientProbe() => Task.FromResult(instanceId.ToString("N")[..8]);
}

public static class StaticActivities
{
    [Activity]
    public static string Greet(string name) => $"Hello from Kogoshvili.Temporal.Hosting, {name}!";

    [Activity]
    public static string StaticProbe() => "static (no instance)";

    [Activity]
    public static int Measure(string payload) => payload.Length;

    [Activity]
    public static string LocalEcho(string value) => value.ToUpperInvariant();

    [Activity]
    public static string Reserve(string orderId) => $"reserved {orderId}";

    [Activity]
    public static string Allocate(string orderId) => $"allocated {orderId}";

    [Activity]
    public static string Charge(string orderId) =>
        throw new InvalidOperationException($"charge failed for {orderId}");

    [Activity]
    public static string CancelReservation(string orderId) => $"cancel-reservation {orderId}";

    [Activity]
    public static string CancelAllocation(string orderId) => $"cancel-allocation {orderId}";
}
