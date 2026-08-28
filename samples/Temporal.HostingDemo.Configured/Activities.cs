using Kogoshvili.Temporal.Codec;
using Kogoshvili.Temporal.Hosting;
using Temporalio.Activities;

namespace Kogoshvili.Temporal.HostingDemo.Configured;

/// <summary>
/// A workflow/activity payload with a per-field secret. The
/// <c>SecretEncryptionInterceptor</c> encrypts <see cref="Ssn"/> on the way out
/// and decrypts it before <see cref="StaticActivities.ProcessPatient"/> reads it,
/// so it stays unreadable in the Temporal UI even after the surrounding payload
/// is decrypted by the codec server.
/// </summary>
public sealed class Patient
{
    public string Name { get; set; } = "";
    public Secret<string> Ssn { get; set; } = new("");
}

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

    [Activity]
    public static string ProcessPatient(Patient patient) =>
        $"processed {patient.Name} (ssn ends {patient.Ssn.Value[^4..]})";
}

/// <summary>Checkpoint recorded on every heartbeat, used to resume on retry.</summary>
public sealed record DownloadProgress(int BytesDownloaded, int TotalBytes);

/// <summary>
/// A long-running activity built on the <see cref="HeartbeatingActivity"/> base:
/// <see cref="HeartbeatingActivity.LoadProgressAsync{T}"/> resumes from the last
/// checkpoint, <see cref="HeartbeatingActivity.StartAutoHeartbeat"/> keeps the
/// activity alive on a background loop (relaying the latest checkpoint rather
/// than an empty ping), and <see cref="HeartbeatingActivity.Heartbeat"/> records
/// each new checkpoint.
/// </summary>
public sealed class DownloadActivities : HeartbeatingActivity
{
    [Activity]
    public async Task<int> DownloadAsync(int totalBytes)
    {
        var progress = await LoadProgressAsync<DownloadProgress>()
            ?? new DownloadProgress(0, totalBytes);

        using var heartbeat = StartAutoHeartbeat();

        while (progress.BytesDownloaded < progress.TotalBytes)
        {
            await Task.Delay(50, CancellationToken);
            progress = progress with { BytesDownloaded = progress.BytesDownloaded + 1 };
            Heartbeat(progress);
        }

        return progress.BytesDownloaded;
    }
}
