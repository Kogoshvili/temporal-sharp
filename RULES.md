# TemporalSharp Rule Catalog

All 30 rules implemented by TemporalSharp, grouped by category. The `Default`
column is the default severity; `off` means the rule is opt-in — disabled by
default and enabled via `.editorconfig` severity.

## Determinism

Deny-listed members in workflow code. Detection target: any method transitively
reachable from a `[WorkflowRun]` method (or any method in a `[Workflow]` class).

| ID | Default | Rule | Replacement |
|---|---|---|---|
| TMP0101 | Error | Wall-clock time: `DateTime.Now` / `DateTime.UtcNow` / `DateTime.Today` / `DateTimeOffset.Now` / `DateTimeOffset.UtcNow` / `TimeZoneInfo.Local` / `Environment.TickCount` / `Environment.TickCount64` | `Workflow.UtcNow` |
| TMP0102 | Error | `Stopwatch` usage | `Workflow.UtcNow` |
| TMP0111 | Error | Sleep/block: `Thread.Sleep` / `Task.Delay` / `Task.DelayAsync` / `Task.Wait` / `.Result` / `.GetAwaiter().GetResult()` | `Workflow.DelayAsync` |
| TMP0121 | Error | Randomness/identity: `Random.Shared` / `new Random()` / `Guid.NewGuid()` | `Workflow.Random` / `Workflow.NewGuid()` |
| TMP0131 | Error | I/O & env: `Environment.GetEnvironmentVariable` / `File.*` / `Directory.*` / `Console.*` / `HttpClient` / sockets / `Process.Start` | pass via activity |
| TMP0141 | Error | Concurrency: `Task.Run` / `TaskFactory.StartNew` / `Thread.Start` / `new Thread(...)` / `ThreadPool.QueueUserWorkItem` / `Parallel.*` / `BackgroundWorker` | `Workflow.ExecuteActivityAsync` / `Workflow.DelayAsync` |
| TMP0142 | Error | Sync/blocking primitives: `Channel<T>` (`ReadAsync`/`WriteAsync`) / `BlockingCollection<T>` / `SemaphoreSlim` / `ManualResetEventSlim` / `Monitor` / `lock` / `Mutex` / `AutoResetEvent` / `ReaderWriterLockSlim` / `SpinWait` / `CountdownEvent` / `Barrier` | await async equivalents / activity |
| TMP0143 | Warning | Raw task scheduling: `Task.WhenAll` / `Task.WhenAny` / `Task.ContinueWith` / `TaskFactory.ContinueWhenAll` / `ContinueWhenAny` / `CancellationTokenSource.CancelAsync()` | `Workflow.WhenAllAsync` / `Workflow.WhenAnyAsync` / `.Cancel()` |
| TMP0151 | Error | Unordered enumeration: `foreach` / `ToList()` / `ToArray()` / `First()` / `Last()` / `.Keys`/`.Values` / slicing over `Dictionary<,>` / `HashSet<T>` / `Hashtable` / `ConcurrentDictionary<,>` / `ISet<T>` (honor `OrderBy`/`Sorted*` as deterministic) | sort / `SortedDictionary` / `OrderBy` |

## Shared-state mutation

| ID | Default | Rule |
|---|---|---|
| TMP1101 | Error | Assignment / `++` / `--` / `+=` to `static` fields from workflow code |
| TMP1102 | Error | `static` field writes when field is `[ThreadStatic]` |
| TMP1103 | Error | `static` property setters from workflow code |
| TMP1104 | Error | Mutation of static collections (`Add`/`Remove`/`Clear`/`TryAdd`…) |

## SDK feature-misuse

| ID | Default | Rule |
|---|---|---|
| TMP2101 | Error | `ActivityOptions`/`LocalActivityOptions` initializer missing `StartToCloseTimeout` **and** `ScheduleToCloseTimeout` (both `TimeSpan?`) |
| TMP2102 | off | `ScheduleToCloseTimeout` set but no `StartToCloseTimeout` |
| TMP2111 | off | String-name overloads (`ExecuteActivityAsync(string, IReadOnlyCollection<object?>, …)`, same for child/local) instead of a typed lambda |
| TMP2121 | Error | `CreateContinueAsNewException(...)` result created but never thrown |
| TMP2131 | Warning | `Console.*` / `System.Diagnostics.Debug` / `Trace` / non-SDK `ILogger` / Serilog / NLog instead of `Workflow.Logger` |
| TMP2141 | Error | `Delegate` / `Action` / `Func<>` / `Channel<T>` / `Stream` / `IAsyncEnumerable<T>` as activity/workflow params or returns |
| TMP2151 | off | Param/property name matches sensitive-data regex |
| TMP2161 | off | Workflow param field mapped to a search attribute but never upserted via `UpsertTypedSearchAttributes` |
| TMP2171 | off | `object`/`dynamic`/`JsonElement` top-level params instead of a concrete type |

## .NET-specific

| ID | Default | Rule |
|---|---|---|
| TMP3101 | Warning | **Heartbeat**: `[Activity]` method with a loop or multiple awaits but no `ActivityExecutionContext.Heartbeat()` call |
| TMP3102 | Error | **Heartbeat**: `HeartbeatTimeout` set on `ActivityOptions` but the activity method never calls `Heartbeat()` |
| TMP3103 | Warning | **Heartbeat**: activity calls `Heartbeat()` but is invoked without a `HeartbeatTimeout` |
| TMP3104 | Warning | **Heartbeat**: activity calls `Heartbeat()` but has no loop and at most one await (heartbeat unnecessary) |
| TMP3201 | Error | SDK-contract sanity: `[WorkflowRun]` not `public` / not `Task`-returning / multiple `[WorkflowRun]` / `[WorkflowRun]` without `[Workflow]` |
| TMP3202 | Error | SDK-contract sanity: `[Activity]` on a non-method / missing `[Activity]` where expected |
| TMP3301 | Error | Versioning: patch id both `Patched` and `DeprecatePatch`'d in the same workflow (leftover) |
| TMP3302 | Warning | Versioning: `Patched` / `DeprecatePatch` id is not a constant string |

> **TMP3103** is best-effort: it can only check the options when the activity is
> invoked via a typed lambda in the same compilation — string-name calls and
> cross-assembly invocations cannot be resolved statically.
