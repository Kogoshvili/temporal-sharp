# Kogoshvili.Temporal Rule Catalog

Rules implemented by the Kogoshvili.Temporal analyzer, grouped by category. The `Default`
column is the default severity; `off` means the rule is opt-in — disabled by
default and enabled via `.editorconfig` severity.

## Determinism

Deny-listed members in workflow code. Detection target: any method transitively
reachable from a `[WorkflowRun]` method (or any method in a `[Workflow]` class).

| ID | Default | Rule | Replacement |
|---|---|---|---|
| TMP0101 | Error | Wall-clock time: `DateTime.Now` / `DateTime.UtcNow` / `DateTime.Today` / `DateTimeOffset.Now` / `DateTimeOffset.UtcNow` / `TimeZoneInfo.Local` / `Environment.TickCount` / `Environment.TickCount64` | `Workflow.UtcNow` |
| TMP0102 | Error | `Stopwatch` usage | `Workflow.UtcNow` |
| TMP0111 | Error | Block: `Thread.Sleep` / `Task.Delay` / `Task.Wait` / `Task.WaitAll` / `Task.WaitAny` / `.Result` / `.GetAwaiter().GetResult()` (incl. `ValueTask` variants) | `await` (or `Workflow.DelayAsync` for delays) |
| TMP0112 | Error | Un-awaited `Task`/`ValueTask` (fire-and-forget) call in workflow code | `await`, or `_ =` discard |
| TMP0113 | Error | `ConfigureAwait(false)` in workflow code (leaves the workflow context) | omit, or `ConfigureAwait(true)` |
| TMP0121 | Error | Randomness/identity: `Random.Shared` / `new Random()` / `Guid.NewGuid()` | `Workflow.Random` / `Workflow.NewGuid()` |
| TMP0131 | Error | I/O & env: `Environment.GetEnvironmentVariable` / `File.*` / `Directory.*` / `Console.*` / `HttpClient` / sockets / `Process.Start` | pass via activity |
| TMP0141 | Error | Concurrency: `Thread.Start` / `new Thread(...)` / `ThreadPool.QueueUserWorkItem` / `Parallel.*` / `BackgroundWorker` | move into an activity |
| TMP0146 | Error | `Task.Run` / `TaskFactory.StartNew` (default task scheduler) | `Workflow.RunTaskAsync` |
| TMP0142 | Error | Sync/blocking primitives: `Channel<T>` (`ReadAsync`/`WriteAsync`) / `BlockingCollection<T>` / `WaitHandle.WaitOne`/`WaitAny`/`WaitAll` / `ManualResetEventSlim` / `ManualResetEvent` / `EventWaitHandle` / `Monitor` / `lock` / `AutoResetEvent` / `ReaderWriterLockSlim` / `ReaderWriterLock` / `SpinWait` / `CountdownEvent` / `Barrier` | move into an activity |
| TMP0147 | Error | `Mutex` / `Semaphore` / `SemaphoreSlim` blocking primitives | `Temporalio.Workflows.Mutex` / `Temporalio.Workflows.Semaphore` |
| TMP0143 | Warning | Raw task scheduling: `Task.WhenAll` / `Task.WhenAny` / `Task.ContinueWith` / `TaskFactory.ContinueWhenAll` / `ContinueWhenAny` / `CancellationTokenSource.CancelAsync()` | `Workflow.WhenAllAsync` / `Workflow.WhenAnyAsync` / `.Cancel()` |
| TMP0144 | Error | Raw task coordination: `new TaskCompletionSource<T>()` (task not owned by the deterministic scheduler) | `Workflow.WaitConditionAsync` on a field |
| TMP0145 | Error | Reflection / dynamic invocation: `Activator.CreateInstance` / `Assembly.Load*` / `Assembly.GetTypes` / `Type.GetType` / `MethodInfo.Invoke` / `Delegate.DynamicInvoke` | move into an activity |
| TMP0151 | Error | Unordered enumeration: `foreach` / `ToList()` / `ToArray()` / `First()` / `Last()` / `.Keys`/`.Values` / slicing over `Dictionary<,>` / `HashSet<T>` / `Hashtable` / `ConcurrentDictionary<,>` / `ISet<T>` (honor `OrderBy`/`Sorted*` as deterministic) | sort / `SortedDictionary` / `OrderBy` |
| TMP0161 | Warning | Culture-sensitive parse/format: `Parse`/`ParseExact`/`TryParse`/`ToString` on numeric/date/time types and `string.Format` without an `IFormatProvider` (uses ambient culture) | pass `CultureInfo.InvariantCulture` |

## Shared-state mutation

| ID | Default | Rule |
|---|---|---|
| TMP1101 | Error | Assignment / `++` / `--` / `+=` to `static` fields from workflow code |
| TMP1102 | Error | `static` field writes when field is `[ThreadStatic]` |
| TMP1103 | Error | `static` property setters from workflow code |
| TMP1104 | Error | Mutation of static collections (`Add`/`Remove`/`Clear`/`TryAdd`…) |
| TMP1105 | Error | Mutation of shared static reference state via a mutating method call (`Set`/`Create`/`Update`/`Write`/`Dispose`…); immutable BCL statics (`Regex`, `JsonSerializerOptions`, …) are excluded |
| TMP1106 | Error | Ambient `AsyncLocal<T>` / `ThreadLocal<T>` declarations and `.Value` access from workflow code |

## SDK feature-misuse

| ID | Default | Rule |
|---|---|---|
| TMP2101 | Error | `ActivityOptions`/`LocalActivityOptions` initializer missing both `StartToCloseTimeout` and `ScheduleToCloseTimeout` (at least one is required) |
| TMP2103 | off | `Workflow.WaitConditionAsync` called without a timeout (can wait forever) |
| TMP2104 | Warning | `Workflow.WaitConditionAsync` timeout result discarded (timeout has no effect) |
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
| TMP3203 | Warning | SDK-contract sanity: `[Activity]` method writes to a mutable instance field/property (races across concurrent executions) |
| TMP3204 | Error | SDK-contract sanity: `[WorkflowQuery]` method is `async` or returns `void`/`Task`/`Task<T>` (queries must be synchronous and return a value) |
| TMP3205 | Error | SDK-contract sanity: `[WorkflowSignal]` method returns `Task<T>` or a value (must return `void` or `Task`) |
| TMP3206 | Error | SDK-contract sanity: `[WorkflowQuery]` method writes to an instance field/property (queries must be read-only) |
| TMP3207 | Error | SDK-contract sanity: Workflow command API (`DelayAsync` / `WaitConditionAsync` / `ExecuteActivityAsync` / …) called inside a `[WorkflowQuery]` |
| TMP3301 | Error | Versioning: patch id both `Patched` and `DeprecatePatch`'d in the same workflow (leftover) |
| TMP3302 | Warning | Versioning: `Patched` / `DeprecatePatch` id is not a constant string |

> **TMP3103** is best-effort: it can only check the options when the activity is
> invoked via a typed lambda in the same compilation — string-name calls and
> cross-assembly invocations cannot be resolved statically.
