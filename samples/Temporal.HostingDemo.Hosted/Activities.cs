using Kogoshvili.Temporal.Hosting;
using Temporalio.Activities;

namespace Kogoshvili.Temporal.HostingDemo.Hosted;

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
}
