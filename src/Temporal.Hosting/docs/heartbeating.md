# Heartbeating activities

`HeartbeatingActivity` is an abstract base for long-running activities that need
to heartbeat progress, stay alive, and resume from a checkpoint after a retry.
Subclasses write their `[Activity]` methods against its protected surface
(`Context`, `CancellationToken`, `Heartbeat`, `LoadProgressAsync<T>`,
`StartAutoHeartbeat`) instead of reaching into
`ActivityExecutionContext.Current` directly.

## Minimal setup

The smallest working subclass resumes from the last attempt's checkpoint,
keeps itself alive on a background heartbeat loop, and records a checkpoint on
every step:

```csharp
using Kogoshvili.Temporal.Hosting;
using Temporalio.Activities;

public sealed record DownloadProgress(int BytesDownloaded, int TotalBytes);

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
```

`LoadProgressAsync<T>()` returns `default` on the first attempt, so `??` seeds a
fresh progress value. `StartAutoHeartbeat()` returns an `IDisposable`; the
`using` spans the activity body and stops the loop on completion or cancellation.
`Heartbeat(progress)` records each checkpoint and remembers it so the background
loop relays the latest value rather than an empty ping. No configuration is
required for any of this.

## Configuration

There is no direct configuration for `HeartbeatingActivity` itself. The
heartbeat timeout — which also drives the auto-heartbeat interval — comes from
`Temporal:ActivityOptions`. The activity must run through an options preset that
sets `HeartbeatTimeout`, via `ActivityOps` with that preset:

```json
{
  "Temporal": {
    "ActivityOptions": {
      "Default": {
        "ScheduleToCloseTimeout": "00:05:00",
        "HeartbeatTimeout": "00:00:30"
      }
    }
  }
}
```

```csharp
[Workflow]
public sealed class DownloadWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(int totalBytes)
    {
        var downloaded = await ActivityOps.ExecuteAsync(
            () => new DownloadActivities().DownloadAsync(totalBytes),
            "long-running");

        return $"Downloaded {downloaded}/{totalBytes} bytes.";
    }
}
```

Without a heartbeat timeout, the auto-heartbeat loop still runs, but it falls
back to a 30s interval and the server will not time the activity out for missed
heartbeats.

## Full configuration

The auto-heartbeat interval is derived from the activity's `HeartbeatTimeout`:
one third of it, clamped to a minimum of 1s, or 30s when no heartbeat timeout is
configured. `StartAutoHeartbeat(TimeSpan?)` accepts an explicit interval to
override that derivation:

```csharp
using var heartbeat = StartAutoHeartbeat(TimeSpan.FromSeconds(5));
```

The SDK throttles heartbeats internally, so the background loop may tick more
often than the server actually receives. The loop relays the last recorded
checkpoint; before the first `Heartbeat` call it sends an empty heartbeat, so a
background tick never clobbers progress a retry depends on.

`Heartbeat(params object?[] details)` takes any number of details, but a single
immutable value is the intended shape, since `LoadProgressAsync<T>()` reads only
the first detail (index 0) of the previous attempt. The progress type is chosen
per call, so one activity class can run several activities with different
progress shapes. `CancellationToken` exposes the activity's cancellation token,
so `Task.Delay` (and any other cancellable work) honors worker shutdown and
timeouts.

A named preset with a longer heartbeat timeout suits genuinely long-running work:

```json
{
  "Temporal": {
    "ActivityOptions": {
      "Presets": {
        "long-running": {
          "ScheduleToCloseTimeout": "00:30:00",
          "HeartbeatTimeout": "00:01:00"
        }
      }
    }
  }
}
```
