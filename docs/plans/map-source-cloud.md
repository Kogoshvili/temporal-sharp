# Plan: `map --source cloud` — runtime topology from a live Temporal server

Status: proposal (not implemented). This document is not committed yet.

## Goal

Build the same `TopologyGraph` the static `map` produces, but from **server
truth** instead of Roslyn: workflow types, activity calls (with the *actual*
routing and options the server observed), child workflows, signals, updates,
nexus operations, and task-queue usage — collected from a reachable Temporal
frontend (Temporal Cloud or self-hosted) via the SDK client the CLI already
uses for `history`.

The two sources are complementary, not competing:

| | `--source code` (today) | `--source cloud` (this plan) |
|---|---|---|
| Answers | "what is wired up in code" | "what actually ran, where, with which settings" |
| Never-run registrations / orphans | ✅ | ❌ invisible |
| Real queue routing, real timeout values | best-effort static | ✅ exact |
| Signal/query/update edges | code-level | observed executions (queries ❌) |
| Provenance | repo/path | namespace |
| Works offline / on unreadable code | ✅ | ❌ needs reachable server + history |

## CLI surface

```text
temporal-sharp map --source cloud [options]

  --source <code|cloud>            Graph source (default: code).
  --endpoint, --namespace          Same Temporal:<...> config as `history`
  --api-key / mTLS                 (appsettings.json + Temporal__* env vars,
                                   incl. azureKeyVault/awsSecretsManager TLS
                                   sources).
  --sample <n>                     Recent executions per workflow type to fetch
                                   (default 10, cap 100).
  --lookback <duration>            Only executions started within this window
                                   (e.g. 30d). Default: no filter.
  --query <visibility-query>       Extra ListWorkflowExecutions filter
                                   (passthrough, e.g. ExecutionStatus = "Running").
  --format / --output              Same emitters as static (mermaid/json/html/
                                   dot/markdown).
```

`--source code` remains the default; a bare `map ./App.sln` is unchanged.
Implementation uses the **SDK client** (`Temporalio.Client`), not shelling out
to the `temporal` binary — same auth path as `history`, no external tool
dependency, Cloud mTLS/API-key support for free. (Alternative rejected:
invoking the CLI as a subprocess — harder auth, brittle output parsing.)

## Collection pipeline

1. **Namespaces** — from config/flag (v1: no namespace enumeration; Cloud
   requires admin ops for listing).
2. **Visibility scan** — paged `ListWorkflowExecutions` (+ `--query`,
   `--lookback`), grouped by `(WorkflowType, TaskQueue)`. Each group → a
   workflow node (execution count kept as node metadata for tooltips).
3. **History sampling** — for each group, fetch the most recent `--sample`
   executions' histories (`GetWorkflowExecutionHistoryAsync`, raw event JSON).
   Sampled per group, so rare-but-observed types still appear.
4. **Event parse** (see table) → nodes/edges merged across the sample: a
   relationship seen in *any* sampled execution becomes a graph edge.
5. **Task queues** — nodes from observed task-queue attributes; enrich with
   `DescribeTaskQueue` poller identity/count (worker presence → queue
   metadata). Boxes as in the static emitter.

## Event → graph mapping

| History event | Graph element |
|---|---|
| `WorkflowExecutionStarted` | Workflow node (registered type name; task queue attr) |
| `ActivityTaskScheduled` | Activity node (registered name) + activity edge with `CallOptions` from the event's **actual** `StartToCloseTimeout` / `ScheduleToCloseTimeout` / `HeartbeatTimeout` / `RetryPolicy`; task-queue edge from the event's actual queue |
| `ActivityTaskHeartbeat` | `Heartbeats` flag on the callee activity node/edge |
| `StartChildWorkflowExecutionInitiated` | `childWorkflow` edge (child type name + its queue) |
| `WorkflowExecutionSignaled` | `signal` handler port on the workflow |
| `SignalExternalWorkflowExecutionInitiated` | `signal` edge to the target workflow (same-namespace v1) |
| `WorkflowExecutionUpdateAccepted` | `update` handler port |
| `NexusOperationScheduled` | `nexus` edge to endpoint/service/operation (string-named → unknown-kind nodes) |

Node identity: registered names, `Id` scheme `Workflow:<ns>:<name>` /
`Activity:<ns>:<name>` (runtime names may collide across namespaces). New
`TopologyNode.Source = code|cloud`; `Repo` = namespace, `Path` = null → the
existing provenance sub-line renders `<ns>:?` per the "no location → ?" rule.

## Coverage & limits (to be stated in output/README)

- **Executed-only, retention-bounded**: registrations that never ran, orphan
  activities, and pre-retention history are invisible. Coverage note is
  rendered with the map ("runtime view: N workflow types, sampling M
  executions, lookback L").
- **Sampling**: rare branches may be missed (`--sample` / `--lookback` /
  `--query` control coverage).
- **Queries** are not recorded in history — query handler names unavailable
  at runtime.
- **Payloads** pass the data converter (codec users see opaque inputs); event
  type names stay plain. No payload decoding in v1.
- **Standalone activities** are workflow-less executions; detection needs the
  activity-executions visibility API (public-preview) — follow-up, not v1.
- Cloud quotas: history fetches are rate-limited; sampling caps keep request
  volume bounded.

## Phases

- **v1** — cloud-only graph, same emitters, coverage note. `Repo` = namespace.
- **v2 (hybrid)** — merge code + cloud graphs keyed by registered names (the
  static name index is the join key): drift report — runtime activity calls
  the static resolver missed, queue routing mismatches (static `TaskQueue`
  evidence vs observed routing), registered-but-never-run nodes, and nodes
  seen at runtime that static analysis cannot attribute.
- **v3 (follow-ups)** — standalone-activity visibility, cross-namespace
  stitching, execution counters as tooltips.

## Testing

- **Event parser**: golden JSON history fixtures (the repo already ships
  golden histories in `Temporal.Testing`) covering each event type, multi-
  sample merging, and option extraction. Pure unit tests, no server.
- **Visibility grouping**: unit tests with fixture list pages.
- **Live integration**: optional test against `temporal server start-dev`,
  gated on an env var (skipped in CI when unset), mirroring existing
  real-SDK test patterns.
- **Emitter reuse**: assert `--source cloud` output renders through the same
  mermaid/markdown emitters (❓ markers for unresolvable names, `?`
  provenance).

## Files touched

- `src/Temporal.Cli/Map/MapOptions.cs` — `--source`, sampling/lookback/query
  options.
- `src/Temporal.Cli/Map/Runtime/` (new) — `RuntimeTopologySource.cs`
  (visibility scan + history fetch + grouping),
  `HistoryEventParser.cs` (event JSON → graph elements).
- `src/Temporal.Cli/Map/MapCommand.cs` — source dispatch; reuses
  `History/` connection-config loader.
- `src/Temporal.Cli/Map/TopologyModel.cs` — `Source` field on nodes.
- `src/Temporal.Cli/README.md` — new section + coverage note.

## Open questions

1. Default `--sample` size (10?) and whether per-type or global budget.
2. Should closed executions be included by default or open-only?
3. For v2 hybrid: is a side-by-side diff artifact (`--diff`) wanted, or merged
   graph with drift annotations?
