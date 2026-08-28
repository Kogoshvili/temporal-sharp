# Kogoshvili.Temporal.Testing

A replay/regression test harness for Temporal .NET workflows. Part of the
[Kogoshvili.Temporal](https://github.com/Kogoshvili/temporal-sharp) tool suite.

Temporal workflows are replayed by re-execution, so a non-deterministic change
(wall-clock time, unordered collection iteration, raw randomness, ...) silently
breaks existing histories. This package makes it easy to catch that in a test:
it starts a real Temporal test environment, runs a workflow to completion,
snapshots its event history, and replays the snapshot through
`WorkflowReplayer`, surfacing any non-determinism.

Unlike the `Kogoshvili.Temporal.Analyzers` package, this library references the
**real** [`Temporalio`](https://www.nuget.org/packages/Temporalio) SDK and
targets **net8.0**.

## API

- **`ReplayHarness : IAsyncDisposable`** — owns a `WorkflowEnvironment` and its
  `ITemporalClient` (exposed as `Environment` / `Client`).
  - `StartTimeSkippingAsync()` / `StartLocalAsync()` — start a time-skipping or
    full local Temporal server (option overloads accept the SDK's
    `WorkflowEnvironmentStartTimeSkippingOptions` / `WorkflowEnvironmentStartLocalOptions`).
  - `CaptureAsync<TWorkflow, TResult>(...)` — run a workflow to completion and
    capture its result and `WorkflowHistory`.
  - `ReplayAsync<TWorkflow>(history)` — replay a history via `WorkflowReplayer`.
  - `VerifyAsync<TWorkflow, TResult>(...)` — capture + replay in one step.
- **`ReplayResult`** — `Succeeded`, `ReplayFailure`, `SnapshotJson`,
  `ThrowIfFailed()`.
- **`Replay`** — replay histories from a fixed source without a local test
  environment:
  - `FromJsonAsync<TWorkflow>(historyJson, workflowId)` — replay one golden history.
  - `FromDirectoryAsync<TWorkflow>(dir, pattern = "*.json")` — replay every
    `*.json` golden file matching `pattern`.
  - `FromServerAsync<TWorkflow>(client, workflowType, executionStatus, limit)` —
    replay recorded histories from a live Temporal service.
- **`Snapshot`** — `ToJson` / `FromJson` / `AssertEquivalent` /
  `AreEquivalent` for JSON snapshot comparison.
- **`ReplayMismatchException`** — thrown on replay divergence or snapshot
  mismatch.

## Replay sources

`Kogoshvili.Temporal.Testing` supports three ways to feed histories into
`WorkflowReplayer`:

1. **Live, local capture** (`ReplayHarness.VerifyAsync`) — starts a bundled
   local Temporal test environment, runs the workflow, and captures its history
   with `FetchHistoryAsync`. No external server or credentials needed.
2. **Checked-in golden files** (`Replay.FromJsonAsync` /
   `Replay.FromDirectoryAsync`) — replay JSON histories exported from the
   Temporal CLI (`temporal workflow show --output json`) or web UI and committed
   to the repo. Ideal for offline/CI regression tests against real shapes.
3. **Live service** (`Replay.FromServerAsync`) — replay recorded histories for a
   workflow type from a running Temporal service, optionally filtered by
   execution status and capped by a total count.

### Authenticating via configuration

For the live-service path, connect using the shared
`Kogoshvili.Temporal.Configuration` project, which reads the `Temporal` section
of `appsettings.json` and `Temporal__*` environment variables:

```csharp
using Kogoshvili.Temporal.Configuration;
using Kogoshvili.Temporal.Testing;

// Connect from appsettings.json + Temporal__* env vars (Cloud mTLS / API key).
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

## Usage

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

The harness can also be used directly with `await using` instead of an xUnit
fixture.

> Note: `StartTimeSkippingAsync` / `StartLocalAsync` lazily download the Temporal
> test-server/dev-server binary on first use, so the first test run needs network
> access. The time-skipping environment is single-test-at-a-time.

Not affiliated with or endorsed by Temporal Technologies.
