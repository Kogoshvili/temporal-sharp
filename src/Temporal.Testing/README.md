# Kogoshvili.Temporal.Testing

A replay/regression test harness for Temporal .NET workflows. It starts a real
Temporal test environment, runs a workflow to completion, snapshots its event
history, and replays it through `WorkflowReplayer` to surface non-determinism.

Unlike `Kogoshvili.Temporal.Analyzers`, this library references the real
[`Temporalio`](https://www.nuget.org/packages/Temporalio) SDK and targets
**net8.0**.

## Minimal setup

Replay a workflow by capturing its history and replaying it in one step. The
harness owns a time-skipping `WorkflowEnvironment` and its client:

```csharp
using Kogoshvili.Temporal.Testing;
using Temporalio.Client;
using Temporalio.Worker;
using Temporalio.Workflows;

[Workflow]
public class GreetingWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        await Workflow.DelayAsync(TimeSpan.FromMilliseconds(1));
        return $"Hello, {name}!";
    }
}

public class ReplayHarnessTests : IAsyncLifetime
{
    private ReplayHarness _harness = null!;

    public async Task InitializeAsync() =>
        _harness = await ReplayHarness.StartTimeSkippingAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task DeterministicWorkflow_Replays_WithoutFailure()
    {
        var result = await _harness.VerifyAsync<GreetingWorkflow, string>(
            new TemporalWorkerOptions("replay-test-queue").AddWorkflow<GreetingWorkflow>(),
            workflow => workflow.RunAsync("world"),
            new WorkflowOptions { Id = "greeting-replay", TaskQueue = "replay-test-queue" });

        Assert.True(result.Succeeded, result.ReplayFailure?.ToString());
    }
}
```

`VerifyAsync` captures the history, replays it, and returns a `ReplayResult`
whose `Succeeded` reflects whether the replay was deterministic. The harness can
also be used directly with `await using` instead of an xUnit fixture.

## Configuration

The local harness is code-only and needs no configuration. Configuration enters
only through the live-service replay path, which connects via the shared
`Kogoshvili.Temporal.Configuration` project reading the `Temporal` section of
`appsettings.json` (plus `Temporal__*` environment variables):

```json
{
  "Temporal": {
    "TargetHost": "my-namespace.tmprl.cloud:7233",
    "Namespace": "my-namespace",
    "Tls": {
      "ClientCertPath": "certs/client.pem",
      "ClientPrivateKeyPath": "certs/client.key"
    }
  }
}
```

```csharp
using Kogoshvili.Temporal.Configuration;
using Kogoshvili.Temporal.Testing;

var client = await TemporalConfig.ConnectAsync();

await foreach (var result in Replay.FromServerAsync<GreetingWorkflow>(
    client,
    workflowType: "GreetingWorkflow",
    executionStatus: "Completed",
    limit: 50))
{
    result.ThrowIfFailed();
}
```

`TemporalConfig.ConnectAsync()` has overloads for no-arg, `IConfiguration`, and
`TemporalConnectionOptions`.

## Full configuration

### Replay sources

There are three ways to feed histories into `WorkflowReplayer`:

1. **Live, local capture** — `ReplayHarness.VerifyAsync` starts a bundled local
   test environment, runs the workflow, and captures its history with
   `FetchHistoryAsync`. No external server or credentials needed.
2. **Checked-in golden files** — `Replay.FromJsonAsync` /
   `Replay.FromDirectoryAsync` replay JSON histories exported from the Temporal
   CLI (`temporal workflow show --output json`) or web UI and committed to the
   repo.
3. **Live service** — `Replay.FromServerAsync` replays recorded histories for a
   workflow type from a running Temporal service, optionally filtered by
   execution status and capped by a total count.

### ReplayHarness

`ReplayHarness : IAsyncDisposable` owns a `WorkflowEnvironment` and its
`ITemporalClient` (exposed as `Environment` / `Client`):

- `StartTimeSkippingAsync()` / `StartLocalAsync()` — start a time-skipping or
  full local Temporal server. Option overloads accept the SDK's
  `WorkflowEnvironmentStartTimeSkippingOptions` / `WorkflowEnvironmentStartLocalOptions`.
- `CaptureAsync<TWorkflow, TResult>(workerOptions, runCall, startOptions)` — run
  a workflow to completion and capture its result and `WorkflowHistory`.
- `ReplayAsync<TWorkflow>(history)` — replay a history via `WorkflowReplayer`,
  returning the raw `WorkflowReplayResult`.
- `VerifyAsync<TWorkflow, TResult>(workerOptions, runCall, startOptions)` —
  capture + replay in one step.

### ReplayResult

The outcome of a capture-and-replay run:

- `Succeeded` — true when the workflow replayed without divergence.
- `ReplayFailure` — the non-determinism detected by `WorkflowReplayer`, or null.
- `SnapshotJson` — the captured history as JSON.
- `ThrowIfFailed()` — throws `ReplayMismatchException` when the replay diverged.

### Snapshot

Helpers for capturing and comparing event-history snapshots:

- `ToJson(history)` / `FromJson(json, workflowId)` — serialize and rehydrate a
  `WorkflowHistory`.
- `AssertEquivalent(expectedJson, actualJson)` — throws
  `ReplayMismatchException` when the snapshots diverge.
- `AreEquivalent(expectedJson, actualJson)` — structural equality ignoring key
  order and insignificant whitespace.

### Edge cases

- `StartTimeSkippingAsync` / `StartLocalAsync` lazily download the Temporal
  test-server/dev-server binary on first use, so the first test run needs
  network access.
- The time-skipping environment is single-test-at-a-time; a full local server is
  available via `StartLocalAsync` when that is a constraint.
- `Replay.FromDirectoryAsync` uses each file name (without extension) as the
  workflow id.
- `Replay.FromServerAsync` returns `IAsyncEnumerable<WorkflowReplayResult>`;
  call `ThrowIfFailed()` per result or inspect `ReplayFailure` to detect
  divergence.
