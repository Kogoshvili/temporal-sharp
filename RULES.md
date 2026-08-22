# TemporalSharp Rule Catalog

All 26 rules implemented by TemporalSharp, grouped by category. Rules marked
`(opt-in)` are disabled by default and enabled via `.editorconfig` severity.

## Determinism

Deny-listed members in workflow code. Detection target: any method transitively
reachable from a `[WorkflowRun]` method (or any method in a `[Workflow]` class).

| ID | Rule | Replacement |
|---|---|---|
| TMP0101 | Wall-clock time: `DateTime.Now` / `DateTime.UtcNow` / `DateTime.Today` / `DateTimeOffset.Now` / `DateTimeOffset.UtcNow` / `TimeZoneInfo.Local` / `Environment.TickCount` / `Environment.TickCount64` | `Workflow.UtcNow` |
| TMP0102 | `Stopwatch` usage | `Workflow.UtcNow` |
| TMP0111 | Sleep/block: `Thread.Sleep` / `Task.Delay` / `Task.DelayAsync` / `Task.Wait` / `.Result` / `.GetAwaiter().GetResult()` | `Workflow.DelayAsync` |
| TMP0121 | Randomness/identity: `Random.Shared` / `new Random()` / `Guid.NewGuid()` | `Workflow.Random` / `Workflow.NewGuid()` |
| TMP0131 | I/O & env: `Environment.GetEnvironmentVariable` / `File.*` / `Directory.*` / `Console.*` / `HttpClient` / sockets / `Process.Start` | pass via activity |
| TMP0141 | Concurrency: `Task.Run` / `TaskFactory.StartNew` / `Thread.Start` / `new Thread(...)` / `ThreadPool.QueueUserWorkItem` / `Parallel.*` / `BackgroundWorker` | `Workflow.ExecuteActivityAsync` / `Workflow.DelayAsync` |
| TMP0142 | Sync/blocking primitives: `Channel<T>` (`ReadAsync`/`WriteAsync`) / `BlockingCollection<T>` / `SemaphoreSlim` / `ManualResetEventSlim` / `Monitor` / `lock` / `Mutex` / `AutoResetEvent` / `ReaderWriterLockSlim` / `SpinWait` / `CountdownEvent` / `Barrier` | await async equivalents / activity |
| TMP0151 | Unordered enumeration: `foreach` over `Dictionary<,>` / `HashSet<T>` / `Hashtable` / `ConcurrentDictionary<,>` / `ISet<T>` (honor `OrderBy`/`Sorted*` as deterministic) | sort / `SortedDictionary` / `OrderBy` |

## Shared-state mutation

| ID | Rule |
|---|---|
| TMP1101 | Assignment / `++` / `--` / `+=` to `static` fields from workflow code |
| TMP1102 | `static` field writes when field is `[ThreadStatic]` |
| TMP1103 | `static` property setters from workflow code |
| TMP1104 | Mutation of static collections (`Add`/`Remove`/`Clear`/`TryAdd`…) |

## SDK feature-misuse

| ID | Rule |
|---|---|
| TMP2101 | `ActivityOptions`/`LocalActivityOptions` initializer missing `StartToCloseTimeout` **and** `ScheduleToCloseTimeout` (both `TimeSpan?`) |
| TMP2102 | `ScheduleToCloseTimeout` set but no `StartToCloseTimeout` `(opt-in)` |
| TMP2111 | String-name overloads (`ExecuteActivityAsync(string, IReadOnlyCollection<object?>, …)`, same for child/local) instead of a typed lambda `(opt-in)` |
| TMP2121 | `CreateContinueAsNewException(...)` result created but never thrown |
| TMP2131 | `Console.*` / `System.Diagnostics.Debug` / `Trace` / non-SDK `ILogger` / Serilog / NLog instead of `Workflow.Logger` |
| TMP2141 | `Delegate` / `Action` / `Func<>` / `Channel<T>` / `Stream` / `IAsyncEnumerable<T>` as activity/workflow params or returns |
| TMP2151 | Param/property name matches sensitive-data regex `(opt-in)` |
| TMP2161 | Workflow param field mapped to a search attribute but never upserted via `UpsertTypedSearchAttributes` `(opt-in)` |
| TMP2171 | `object`/`dynamic`/`JsonElement` top-level params instead of a concrete type `(opt-in)` |

## .NET-specific

| ID | Rule |
|---|---|
| TMP3101 | **Heartbeat**: `[Activity]` method with a loop or multiple awaits but no `ActivityExecutionContext.Heartbeat()` call |
| TMP3102 | **Heartbeat**: `HeartbeatTimeout` set on `ActivityOptions` but the activity method never calls `Heartbeat()` |
| TMP3201 | SDK-contract sanity: `[WorkflowRun]` not `public` / not `Task`-returning / multiple `[WorkflowRun]` / `[WorkflowRun]` without `[Workflow]` |
| TMP3202 | SDK-contract sanity: `[Activity]` on a non-method / missing `[Activity]` where expected |
| TMP3301 | Versioning misuse: `Patched` / `DeprecatePatch` usage anomalies |
