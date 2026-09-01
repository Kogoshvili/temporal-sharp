# Kogoshvili.Temporal.Testing

A replay/regression test harness for Temporal .NET workflows. It starts a real
Temporal test environment, runs a workflow to completion, snapshots its event
history, and replays it through `WorkflowReplayer` to surface non-determinism.

Unlike `Kogoshvili.Temporal.Analyzers`, this library references the real
[`Temporalio`](https://www.nuget.org/packages/Temporalio) SDK and targets
**net8.0**.

## Minimal setup

Replay recorded histories for a workflow type from a live Temporal service —
Cloud or self-hosted. The replayer registers the workflow type; the connection
comes from the shared `Kogoshvili.Temporal.Configuration` project (see
[Configuration](#configuration)):

```csharp
using Kogoshvili.Temporal.Configuration;
using Kogoshvili.Temporal.Testing;
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

## Configuration

The live-service replay path connects via the shared
`Kogoshvili.Temporal.Configuration` project, which reads the `Temporal` section
of `appsettings.json` (plus `Temporal__*` environment variables):

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

`TemporalConfig.ConnectAsync()` has overloads for no-arg, `IConfiguration`, and
`TemporalConnectionOptions`.

## Golden files

Replay JSON histories checked into the repo — exported with the Temporal CLI
(`temporal workflow show --output json`), downloaded with the `temporal-sharp`
CLI, or exported from the web UI. No server, test environment, or credentials
needed at test time — only the workflow type under test.

`history download` fetches recorded histories from a live service using the
same shared configuration as the replay path (`appsettings.json` +
`Temporal__*` environment variables) and writes one `*.json` file per workflow
id:

```shell
temporal-sharp history download GreetingWorkflow --out histories --limit 50
```

```csharp
using Kogoshvili.Temporal.Testing;

// One exported history; the workflow id is passed explicitly
var result = await Replay.FromJsonAsync<GreetingWorkflow>(
    await File.ReadAllTextAsync("histories/greeting-replay.json"),
    workflowId: "greeting-replay");
result.ThrowIfFailed();

// Every JSON history in a directory; file names (minus extension) become workflow ids
foreach (var result in await Replay.FromDirectoryAsync<GreetingWorkflow>("histories"))
{
    result.ThrowIfFailed();
}
```

## Full configuration

### Replay sources

There are three ways to feed histories into `WorkflowReplayer`:

1. **Live service** — `Replay.FromServerAsync` replays recorded histories for a
   workflow type from a running Temporal service, optionally filtered by
   execution status and capped by a total count.
2. **Checked-in golden files** — `Replay.FromJsonAsync` /
   `Replay.FromDirectoryAsync` replay JSON histories exported from the Temporal
   CLI (`temporal workflow show --output json`) or web UI and committed to the
   repo.
3. **Live, local capture** — `ReplayHarness.VerifyAsync` starts a bundled local
   test environment, runs the workflow, and captures its history with
   `FetchHistoryAsync`. No external server or credentials needed.

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
