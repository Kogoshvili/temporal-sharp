using Temporalio.Activities;

namespace Kogoshvili.Temporal.HostingDemo.Raw;

// The same four activity lifetimes as the "hosted" demo, but expressed without
// [ActivityLifetime]: the raw SDK has no such attribute, so the lifetime is
// chosen by whichever AddXxxActivities call you make in Program.cs.

public sealed class ScopedActivities
{
    private readonly Guid instanceId = Guid.NewGuid();

    [Activity]
    public Task<string> ScopedProbe() => Task.FromResult(instanceId.ToString("N")[..8]);
}

public sealed class SingletonActivities
{
    private readonly Guid instanceId = Guid.NewGuid();

    [Activity]
    public Task<string> SingletonProbe() => Task.FromResult(instanceId.ToString("N")[..8]);
}

public sealed class TransientActivities
{
    private readonly Guid instanceId = Guid.NewGuid();

    [Activity]
    public Task<string> TransientProbe() => Task.FromResult(instanceId.ToString("N")[..8]);
}

public static class StaticActivities
{
    [Activity]
    public static string Greet(string name) => $"Hello from the raw Temporal SDK, {name}!";

    [Activity]
    public static string StaticProbe() => "static (no instance)";
}
