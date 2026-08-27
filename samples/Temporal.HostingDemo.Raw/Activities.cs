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

    [Activity]
    public static int Measure(string payload) => payload.Length;

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

/// <summary>Checkpoint recorded on every heartbeat, used to resume on retry.</summary>
public sealed record DownloadProgress(int BytesDownloaded, int TotalBytes);

/// <summary>
/// The hand-rolled equivalent of the starter's <see cref="HeartbeatingActivity"/>:
/// resume is read from <c>ActivityInfo.HeartbeatDetailAtAsync</c>, heartbeats are
/// recorded manually each iteration (no background auto-heartbeat), and
/// cancellation is checked on the context token directly.
/// </summary>
public static class ManualHeartbeatActivities
{
    [Activity]
    public static async Task<int> DownloadAsync(int totalBytes)
    {
        var ctx = ActivityExecutionContext.Current;

        var progress = ctx.Info.HeartbeatDetails.Count > 0
            ? await ctx.Info.HeartbeatDetailAtAsync<DownloadProgress>(0)
            : new DownloadProgress(0, totalBytes);

        while (progress.BytesDownloaded < progress.TotalBytes)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(50);
            progress = progress with { BytesDownloaded = progress.BytesDownloaded + 1 };
            ctx.Heartbeat(progress);
        }

        return progress.BytesDownloaded;
    }
}
