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
  `ITemporalClient`.
  - `StartTimeSkippingAsync()` / `StartLocalAsync()` — start a time-skipping or
    full local Temporal server.
  - `CaptureAsync<TWorkflow, TResult>(...)` — run a workflow to completion and
    capture its `WorkflowHistory`.
  - `ReplayAsync<TWorkflow>(history)` — replay a history via `WorkflowReplayer`.
  - `VerifyAsync<TWorkflow, TResult>(...)` — capture + replay in one step.
- **`ReplayResult`** — `Succeeded`, `ReplayFailure`, `SnapshotJson`,
  `ThrowIfFailed()`.
- **`Replay`** — replay histories from a fixed source without a local test
  environment:
  - `FromJsonAsync<TWorkflow>(json, workflowId)` — replay one golden history.
  - `FromDirectoryAsync<TWorkflow>(dir)` — replay every `*.json` golden file.
  - `FromServerAsync<TWorkflow>(client, workflowType)` — replay recorded
    histories from a live Temporal service.
- **`Snapshot`** — `ToJson` / `FromJson` / `AssertEquivalent` for JSON snapshot
  comparison.
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
   workflow type from a running Temporal service. Supply your own authenticated
   `ITemporalClient` (Cloud mTLS or API key); authentication is up to the caller.

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
