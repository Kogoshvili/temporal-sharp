# POC: Temporal testing rules + replay/regression harness

This document describes a proof-of-concept that ships the **TMP5xxx "Testing"**
rule family in the `Kogoshvili.Temporal` analyzer and introduces a new
**`Temporal.Testing`** replay/regression test harness. It is scoped work tracked
in `plan.md`; everything here is intentionally small and reviewable.

- **Part A** adds three opt-in analyzer rules that detect missing replay/teardown
  coverage around Temporal workflows and test environments.
- **Part B** adds a library (`src/Temporal.Testing`) that runs a workflow against
  a real Temporal test environment, snapshots its event history, and replays it
  through `WorkflowReplayer` to surface non-determinism.

A runnable [`demo.sh`](demo.sh) at the repo root demonstrates both parts. The
captured output is reproduced in the [Demo](#demo) section at the bottom.

---

## Part A — TMP5xxx analyzer rules

Three new rules live in a new `TestingAnalyzer`
(`src/Temporal.Analyzers/Analyzers/TestingAnalyzer.cs`), with descriptors in
`src/Temporal.Analyzers/Diagnostics/DiagnosticDescriptors.cs` (the single source
of truth). They are all in the `Testing` category and are **opt-in** —
`isEnabledByDefault: false`, i.e. `off` by default, exactly as scoped in
`plan.md`.

| ID | Detects | Default |
|---|---|---|
| `TMP5001` | A compilation declares `[Workflow]` type(s) but never references `WorkflowReplayer` (no `new WorkflowReplayer(...)` anywhere), so no replay test covers them. | `off` |
| `TMP5002` | A `WorkflowEnvironment` / `TestWorkflowEnvironment` local that is neither scoped with `using`/`await using` nor disposed via `Dispose` / `DisposeAsync` / `ShutdownAsync`. | `off` |
| `TMP5003` | A `WorkflowEnvironment` is used but no `TemporalWorker.ExecuteAsync(...)` call is found, so the worker never actually runs workflows. | `off` |

### Detection approach: match by name, no SDK reference

Like the rest of the engine, the rules never reference the Temporal SDK assembly.
They match purely by name:

- `[Workflow]` types are detected via `WorkflowDetection.IsWorkflowType`, which
  matches an attribute whose class name is `Temporalio.Workflows.WorkflowAttribute`.
- `WorkflowReplayer` is detected by an `ObjectCreationExpression` whose type name
  is `WorkflowReplayer` (rightmost identifier, so both `new WorkflowReplayer(...)`
  and `new Temporalio.Worker.WorkflowReplayer(...)` match).
- The environment local is detected by the declared local's type name
  (`WorkflowEnvironment` or `TestWorkflowEnvironment`), which works through
  `var` because the semantic model infers the type.
- `ExecuteAsync` is detected as an invocation whose target method is named
  `ExecuteAsync` on a containing type named `TemporalWorker`.

`TMP5001` and `TMP5003` are compilation-wide and are reported once per
compilation (on the first `[Workflow]` type / first environment usage);
`TMP5002` is reported per offending local. The analyzer uses per-compilation
state collected across concurrently-registered actions and reported at
compilation end, mirroring the existing analyzers' patterns.

### Enabling the rules

Via `.editorconfig` (the same mechanism the rest of the engine uses):

```ini
[*.cs]
dotnet_diagnostic.TMP5001.severity = warning
dotnet_diagnostic.TMP5002.severity = warning
dotnet_diagnostic.TMP5003.severity = warning
```

Or on the CLI, per-run, without touching any file:

```sh
temporal-sharp analyze ./App.sln --severity TMP5001=warning --severity TMP5002=warning --severity TMP5003=warning
```

The `strict` preset (`editorconfig/strict.editorconfig`) promotes all three to
`error`; the `recommended` preset leaves them `none`.

### Wiring into the existing engine

Adding a rule required the standard coordinated edits enforced by
`ConsistencyTests` and `DocumentationSyncTests`: descriptors in
`DiagnosticDescriptors.cs`, registration in `TestingAnalyzer.SupportedDiagnostics`,
an entry in `AnalyzerReleases.Unshipped.md`, and a regenerated `RULES.md`
(plus `editorconfig/*.editorconfig` presets). The CLI discovers analyzers by
reflection over the analyzer assembly (`AnalysisRunner.Analyzers`), so the new
`TestingAnalyzer` is picked up by both the NuGet analyzer package and the CLI
with no further wiring. `RULES.md` now has a `Testing` section listing the three
rules as `off`.

---

## Part B — `Temporal.Testing` replay/regression harness

`src/Temporal.Testing` is a net8.0 library that references the **real**
`Temporalio` NuGet package (unlike the analyzer). It provides an xUnit-friendly
fixture/helper for replay-based regression testing.

### API

**`ReplayHarness : IAsyncDisposable`** — owns a `WorkflowEnvironment` and its
client.

- `ReplayHarness.StartTimeSkippingAsync()` / `StartLocalAsync()` (plus overloads
  taking options) — start a time-skipping or full local Temporal server.
- `Client` — the environment's `ITemporalClient`.
- `CaptureAsync<TWorkflow, TResult>(workerOptions, runCall, startOptions)` —
  starts a workflow, runs a `TemporalWorker` until the workflow completes, and
  returns the result together with the captured `WorkflowHistory`.
- `ReplayAsync<TWorkflow>(history)` — replays a captured history through
  `WorkflowReplayer` and returns the raw `WorkflowReplayResult`.
- `VerifyAsync<TWorkflow, TResult>(...)` — capture + replay in one step,
  returning a `ReplayResult`.
- `DisposeAsync()` — shuts the environment down.

**`ReplayResult`** — `Succeeded` (true when replay was deterministic),
`ReplayFailure` (the exception produced by the replayer, or null), `SnapshotJson`
(the captured history as JSON), and `ThrowIfFailed()`.

**`Snapshot`** — `ToJson(WorkflowHistory)`, `FromJson(json, id)`,
`AssertEquivalent(expectedJson, actualJson)` / `AreEquivalent(...)` for a
structural JSON snapshot comparison.

**`ReplayMismatchException`** — thrown on replay divergence or snapshot
mismatch; carries the original replay failure.

### Example usage

The test project defines a trivial workflow and exercises the harness through an
xUnit `IAsyncLifetime` fixture (`tests/Temporal.Testing.Tests/`):

```csharp
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

    public async Task InitializeAsync()
    {
        _harness = await ReplayHarness.StartTimeSkippingAsync();
    }

    public async Task DisposeAsync()
    {
        await _harness.DisposeAsync();
    }

    [Fact]
    public async Task DeterministicWorkflow_Replays_WithoutFailure()
    {
        var workerOptions = new TemporalWorkerOptions("replay-test-queue")
            .AddWorkflow<GreetingWorkflow>();

        var result = await _harness.VerifyAsync<GreetingWorkflow, string>(
            workerOptions,
            workflow => workflow.RunAsync("world"),
            new WorkflowOptions { Id = "greeting-replay", TaskQueue = "replay-test-queue" });

        Assert.True(result.Succeeded, result.ReplayFailure?.ToString());
    }
}
```

The same harness works via `await using` without a fixture class:

```csharp
await using var harness = await ReplayHarness.StartTimeSkippingAsync();
var result = await harness.VerifyAsync<GreetingWorkflow, string>(...);
Assert.True(result.Succeeded, result.ReplayFailure?.ToString());
```

### How replay/snapshot-diff catches non-determinism

Temporal workflows are replayed by re-execution, so a workflow that is
non-deterministic (wall-clock time, `DateTime.Now`, unordered collection
iteration, etc.) produces different commands on replay than the original run.
`WorkflowReplayer.ReplayWorkflowAsync(history, throwOnReplayFailure: false)`
replays a captured history through the current workflow code and reports a
`ReplayFailure` when the code diverges from the recorded events. The harness
surfaces that failure via `ReplayResult.ReplayFailure` / `ThrowIfFailed()`,
which fails the test. The JSON snapshot (`SnapshotJson`) additionally lets a test
persist a golden history and compare it structurally across runs with
`Snapshot.AssertEquivalent`.

---

## Relationship to the existing rule engine and `plan.md`

- `plan.md` scoped a `Testing (TMP5xxx, off by default, separate detection scope)`
  family and noted that the `Testing` category constant already existed in
  `DiagnosticDescriptors.cs`. Part A lands that family: three rules, all `off`
  by default, in a dedicated analyzer.
- Part B is the runtime counterpart: where TMP5001 warns that a replay test is
  missing, `Temporal.Testing` is the harness that makes writing one easy. The
  analyzer is static and name-based; the harness is dynamic and exercises the
  real SDK's replayer.

## Limitations / POC scope

- **No test-project detection.** The rules fire in any compilation that contains
  the matching type names — they do not yet distinguish test projects from
  production projects, so a `[Workflow]` in a production assembly with no
  replayer anywhere would also raise TMP5001. That matches `plan.md`'s note that
  test-project scoping is follow-up work.
- **Heuristic matching.** `TMP5002` tracks disposal only within the enclosing
  method (a `DisposeAsync` in a helper isn't seen); `TMP5003` keys off the
  method name `ExecuteAsync` on a type named `TemporalWorker`. `TMP5001` reports
  once per compilation rather than per workflow type.
- **Harness covers workflows only.** Activities, signals, queries, and updates
  are out of scope for this POC; `CaptureAsync` runs a single workflow to
  completion.
- **Environment needs a download on first use.** `StartTimeSkippingAsync` /
  `StartLocalAsync` lazily download the Temporal test-server/dev-server binary on
  first run, so the first test execution needs network access. The time-skipping
  environment is documented by the SDK as single-test-at-a-time.
- **Snapshot compare is structural JSON**, not a real Temporal history diff; the
  authoritative non-determinism signal is `WorkflowReplayer`'s `ReplayFailure`.

---

## Demo

Run [`demo.sh`](demo.sh) from the repo root. It (1) builds the solution,
(2) runs the `Temporal.Testing.Tests` harness tests — demonstrating a real
time-skipping replay — and (3) runs the CLI against a tiny
`demo/ReplaylessWorkflow/` project to show `TMP5001` firing (and its absence when
the opt-in rule is not enabled).

```sh
./demo.sh
```

Exact captured output:

```
==> .NET SDK
8.0.424

==> Build the solution (analyzers, CLI, and the Temporal.Testing harness)
  Determining projects to restore...
  All projects are up-to-date for restore.
  Temporal.Analyzers -> /home/lotus/Projects/temporal-experiments-worktrees/testing/src/Temporal.Analyzers/bin/Debug/netstandard2.0/Temporal.Analyzers.dll
  Temporal.Testing -> /home/lotus/Projects/temporal-experiments-worktrees/testing/src/Temporal.Testing/bin/Debug/net8.0/Temporal.Testing.dll
  Temporal.Cli -> /home/lotus/Projects/temporal-experiments-worktrees/testing/src/Temporal.Cli/bin/Debug/net8.0/Temporal.Cli.dll
  Temporal.Testing.Tests -> /home/lotus/Projects/temporal-experiments-worktrees/testing/tests/Temporal.Testing.Tests/bin/Debug/net8.0/Temporal.Testing.Tests.dll
  Temporal.Analyzers.Tests -> /home/lotus/Projects/temporal-experiments-worktrees/testing/tests/Temporal.Analyzers.Tests/bin/Debug/net8.0/Temporal.Analyzers.Tests.dll
  Temporal.Cli.Tests -> /home/lotus/Projects/temporal-experiments-worktrees/testing/tests/Temporal.Cli.Tests/bin/Debug/net8.0/Temporal.Cli.Tests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:01.72

==> Run the replay/regression harness tests
    (starts a time-skipping WorkflowEnvironment, runs a [Workflow] to
     completion, snapshots its history, and replays it via WorkflowReplayer)
  Determining projects to restore...
  All projects are up-to-date for restore.
  Temporal.Testing -> /home/lotus/Projects/temporal-experiments-worktrees/testing/src/Temporal.Testing/bin/Debug/net8.0/Temporal.Testing.dll
  Temporal.Testing.Tests -> /home/lotus/Projects/temporal-experiments-worktrees/testing/tests/Temporal.Testing.Tests/bin/Debug/net8.0/Temporal.Testing.Tests.dll
Test run for /home/lotus/Projects/temporal-experiments-worktrees/testing/tests/Temporal.Testing.Tests/bin/Debug/net8.0/Temporal.Testing.Tests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.11.1 (x64)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     2, Skipped:     0, Total:     2, Duration: 142 ms - Temporal.Testing.Tests.dll (net8.0)

==> Demonstrate TMP5001 firing
    (a [Workflow] with no WorkflowReplayer replay test; TMP5001 is opt-in,
     so it is enabled here via --severity)
/home/lotus/Projects/temporal-experiments-worktrees/testing/demo/ReplaylessWorkflow/GreetingWorkflow.cs(7,14): warning TMP5001: [Workflow] type 'GreetingWorkflow' has no WorkflowReplayer-based replay test

==> Same project without --severity (TMP5001 is off by default -> no output)

Demo complete.
```
