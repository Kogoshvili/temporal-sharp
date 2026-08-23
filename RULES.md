# Kogoshvili.Temporal Rule Catalog

Rules implemented by the Kogoshvili.Temporal analyzer, grouped by category. The `Default`
column is the default severity; `off` means the rule is opt-in — disabled by
default and enabled via `.editorconfig` severity.

Severity follows a simple philosophy: **Error** for replay-breaking determinism and
correctness violations, **Warning** for heuristics and style, and **off** for opt-in
rules that users must enable explicitly.

<!-- This file is generated from DiagnosticDescriptors.cs by `temporal-sharp docs`. Do not edit by hand. -->

## Determinism

| ID | Default | Rule | Description |
|---|---|---|---|
| TMP0101 | Error | Workflow code depends on wall-clock time | Workflow code is replayed by re-execution; reading wall-clock time during replay produces different results. Use Workflow.UtcNow. |
| TMP0102 | Error | Workflow code measures elapsed wall-clock time | A Stopwatch reads elapsed wall-clock time, which differs on replay. Use Workflow.UtcNow to read deterministic time. |
| TMP0104 | Warning | Workflow time compared to a persisted value | Comparing Workflow.UtcNow against an externally persisted timestamp or expiry makes the branch replay-dependent. Use a workflow timer, or persist the comparison time as workflow state. |
| TMP0111 | Error | Workflow code blocks the workflow thread | Sleeping on wall-clock time or synchronously waiting on a task breaks replay determinism and deadlocks the single-threaded workflow runtime. Await asynchronously, and use Workflow.DelayAsync for delays. |
| TMP0112 | Error | Workflow code discards an un-awaited task | Fire-and-forget task calls in workflow code are not tracked by the deterministic scheduler, so their completion is not journaled and replays diverge. Await the task, assign it, or discard it explicitly. |
| TMP0113 | Error | Workflow code uses ConfigureAwait(false) | ConfigureAwait(false) abandons the workflow's synchronization context, so continuations run on the default task scheduler rather than the deterministic workflow scheduler. Omit the call or pass true. |
| TMP0121 | Error | Workflow code uses non-deterministic randomness | Random values generated in workflow code differ on replay. Use Workflow.Random or Workflow.NewGuid. |
| TMP0122 | Error | Workflow code generates cryptographic randomness | Cryptographic RNG reads from OS entropy, which differs on replay. Move crypto-random generation into an activity, or use Workflow.Random for non-cryptographic randomness. |
| TMP0123 | Warning | Workflow randomness used for a persisted id or payload | Workflow.Random and Workflow.NewGuid are deterministic across replays, which makes them unsuitable for identifiers or payloads that must be unique across executions. Generate them in an activity or on the client. |
| TMP0131 | Error | Workflow code performs I/O or reads the environment | I/O and environment access break replay determinism. Perform I/O in an activity. |
| TMP0141 | Error | Workflow code starts concurrent work | Starting threads or parallel work in workflow code breaks replay determinism and has no Workflow.* replacement. Delegate the work to an activity. |
| TMP0142 | Error | Workflow code uses a blocking synchronization primitive | Blocking on locks, channels, or other synchronization primitives can deadlock the single-threaded workflow runtime and break determinism. |
| TMP0143 | Warning | Workflow code uses raw task scheduling | Raw Task combinators (WhenAll/WhenAny/ContinueWith) schedule continuations on the default TaskScheduler rather than the deterministic workflow scheduler. Prefer Workflow.WhenAllAsync / Workflow.WhenAnyAsync, and use .Cancel() instead of CancellationTokenSource.CancelAsync(). |
| TMP0144 | Error | Workflow code uses raw task coordination | TaskCompletionSource produces a Task whose completion and continuation timing are controlled by the default scheduler rather than the replay-deterministic workflow scheduler. Store a field and await it with Workflow.WaitConditionAsync. |
| TMP0145 | Error | Workflow code uses reflection or dynamic invocation | Reflection and dynamic invocation run arbitrary, non-journaled code on the workflow path, so side effects re-run on replay. Delegate this work to an activity. |
| TMP0146 | Error | Workflow code starts work on the default task scheduler | Task.Run and TaskFactory.StartNew schedule work on the default task scheduler rather than the deterministic workflow scheduler. Use Workflow.RunTaskAsync. |
| TMP0147 | Error | Workflow code uses a blocking synchronization primitive with a deterministic replacement | System.Threading.Mutex, Semaphore, and SemaphoreSlim block the single-threaded workflow runtime. Use the SDK's Temporalio.Workflows.Mutex / Temporalio.Workflows.Semaphore, which integrate with the deterministic scheduler. |
| TMP0148 | Info | Workflow code uses Task.WhenAll instead of Workflow.WhenAllAsync | Task.WhenAll currently runs on the workflow scheduler, but the wrapper is the supported, forward-compatible API. Prefer Workflow.WhenAllAsync. |
| TMP0151 | Error | Workflow code iterates a collection in non-deterministic order | Dictionary and HashSet iteration order is not deterministic across runs and replays. Sort the collection first. |
| TMP0161 | Warning | Workflow code parses or formats using the ambient culture | Parsing or formatting numbers, dates, and times with the ambient culture diverges across workers and replays. Pass CultureInfo.InvariantCulture (or another explicit IFormatProvider). |
| TMP0171 | Error | Workflow type declares a finalizer | Finalizers run on GC timing, which is non-deterministic and can run during replay. Move cleanup to an activity or a deterministic shutdown path. |
| TMP0172 | Error | Workflow code schedules a wall-clock timer | System timers fire on wall-clock intervals and do not integrate with the deterministic workflow scheduler. Use Workflow.DelayAsync, or run timer-driven work in an activity. |
| TMP0174 | Error | Workflow code uses a weak reference | WeakReference and ConditionalWeakTable depend on GC timing, which is non-deterministic during replay. Avoid weak references in workflow code. |
| TMP0175 | Warning | Workflow control flow depends on time or randomness | Control flow that depends on wall-clock time or non-deterministic randomness can diverge on replay. Use the deterministic SDK sources, or make the decision in an activity. |
| TMP0177 | Error | Workflow module performs side effects at load | Static constructors, static field initializers, and module initializers run at type/module load, which is not deterministic or journaled. Avoid scheduling workflow commands from them. |

## Shared-state mutation

| ID | Default | Rule | Description |
|---|---|---|---|
| TMP1101 | Error | Workflow code mutates static state | Workflow instances may be replayed or run concurrently; mutating static state produces different results across executions. |
| TMP1102 | Error | Workflow code mutates [ThreadStatic] state | [ThreadStatic] state is per-thread and not deterministic during workflow replay. |
| TMP1103 | Error | Workflow code sets a static property | Mutating static state from workflow code breaks replay determinism and races across executions. |
| TMP1104 | Error | Workflow code mutates a static collection | Workflow instances may be replayed or run concurrently; mutating a static collection produces different results across executions. |
| TMP1105 | Error | Workflow code mutates static state via a method call | Calling a mutating method on a shared static reference changes state visible to every workflow execution. Keep workflow state instance-local. |
| TMP1106 | Error | Workflow code uses ambient AsyncLocal/ThreadLocal state | AsyncLocal/ThreadLocal state lives in ExecutionContext/thread storage and flows across awaits in scheduler-dependent ways, so it is not deterministic replay state. Thread state explicitly through method parameters and fields. |

## SDK feature-misuse

| ID | Default | Rule | Description |
|---|---|---|---|
| TMP2101 | Error | Activity options missing required timeout | Temporal requires at least one of StartToCloseTimeout or ScheduleToCloseTimeout on ActivityOptions and LocalActivityOptions. |
| TMP2103 | off | WaitConditionAsync called without a timeout | Waiting on a condition without a timeout can leave the workflow blocked forever if the signal never arrives. Use the timeout overload and handle the returned bool. |
| TMP2104 | Warning | WaitConditionAsync timeout result ignored | When the bool returned by the timeout overload is discarded, the timeout has no effect and the workflow proceeds as if the condition were met. Check the result and handle the timeout path. |
| TMP2106 | Warning | Retry policy allows retries on a non-idempotent activity | Retrying a non-idempotent activity can duplicate its side effects. Only set a RetryPolicy on idempotent activities. |
| TMP2107 | Warning | Non-idempotent activity called without an idempotency-key argument | A non-idempotent activity needs an idempotency key so retries do not duplicate its side effects. Pass an idempotency key as an argument. |
| TMP2111 | off | Workflow target named by string | String-named targets cannot be resolved statically and bypass compile-time type checking. This overload is legitimate for dynamic workflows, so the rule is opt-in. |
| TMP2121 | Error | Continue-as-new exception is not thrown | CreateContinueAsNewException returns an exception that must be thrown to trigger continue-as-new. |
| TMP2122 | Warning | Continue-as-new without passing current workflow state | Continue-as-new starts a fresh execution; any state needed by the new run must be passed as arguments or it is lost. |
| TMP2123 | Warning | Cancellation is swallowed | Catching a cancellation without rethrowing or checking Workflow.IsCancellationRequested hides a workflow/activity cancellation and can leave work running after cancellation. |
| TMP2124 | Warning | Cleanup after cancellation is not in a non-cancellable scope | Cleanup that runs after cancellation should not itself be cancelled; pass CancellationToken.None (or a token from a detached CancellationTokenSource) to the cleanup work so it always completes. |
| TMP2125 | Warning | Unbounded loop without a continue-as-new check | An unbounded loop that never checks Workflow.ContinueAsNewSuggested grows the workflow history until it hits the size limit. Break the loop and continue as new. |
| TMP2131 | Warning | Non-replay-aware logging in workflow code | Standard loggers write on every replay. Use Workflow.Logger, which suppresses output during replay. |
| TMP2132 | Warning | Non-ApplicationFailureException thrown from workflow code | A non-ApplicationFailureException from a workflow silently retries the task forever. Throw Temporalio.Exceptions.ApplicationFailureException with a typed message. |
| TMP2133 | Warning | Debug/Trace assert in workflow code | Debug.Assert and Trace.Assert are compiled out in release builds and have no place in production workflow code. |
| TMP2141 | Error | Non-serializable type in workflow/activity signature | Workflow and activity arguments and return values are serialized; delegates, streams, channels, and async enumerables cannot round-trip. |
| TMP2142 | Warning | BigInteger in a payload without a converter | System.Numerics.BigInteger is not supported by the default JSON payload converter. Register a custom converter or use a supported type. |
| TMP2143 | Warning | Exception used as a payload | Serializing an Exception as a payload is lossy and couples the workflow contract to the .NET exception hierarchy. Use ApplicationFailure or a plain error model. |
| TMP2144 | Warning | Oversized inline payload | Large literals or collection initializers passed to activities or persisted to state bloat event history. Move them to a field, an activity, or external storage. |
| TMP2146 | Error | Use of internal Temporal namespace | Temporalio.Bridge is not part of the public API and can change without notice. |
| TMP2147 | off | Unsafe namespace imported in workflow code | Importing namespaces that provide I/O, networking, or other non-deterministic APIs into workflow code invites replay bugs. Configure kogoshvili.temporal.unsafe_namespaces with a list of namespace prefixes that workflow code must not import. |
| TMP2151 | off | Workflow/activity parameter or property may contain sensitive data | Workflow inputs are recorded in event history; avoid passing sensitive values directly. |
| TMP2161 | off | Search attribute is never upserted | A search attribute set at workflow start is only indexed then. Upsert it with Workflow.UpsertTypedSearchAttributes when its value changes. |
| TMP2162 | Warning | Search attributes upserted inside a loop | Upserting search attributes on every loop iteration writes a command to history each time. Upsert once after the loop, or only when the value changes. |
| TMP2163 | Warning | Search attribute removal uses the wrong shape | Removing a search attribute requires SearchAttributeKey<T>.ValueUnset(); ValueSet(null) does not remove the attribute. |
| TMP2171 | off | Lossy-number parameter in workflow/activity signature | object/dynamic values decode to JsonElement, and large integers may lose precision. Declare a concrete parameter type. |
| TMP2172 | Warning | Lossy-number member in a workflow/activity payload type | object/dynamic/JsonElement members decode to JsonElement and may lose precision. Declare a concrete member type on the payload DTO. |

## .NET-specific

| ID | Default | Rule | Description |
|---|---|---|---|
| TMP3101 | Warning | Long-running activity does not heartbeat | Long-running activities should heartbeat so Temporal can detect failures and deliver cancellations. |
| TMP3102 | Error | HeartbeatTimeout set but activity never heartbeats | HeartbeatTimeout requires the activity to record heartbeats; otherwise the activity will be considered failed. |
| TMP3103 | Warning | Heartbeat called without HeartbeatTimeout | Without a HeartbeatTimeout, heartbeat calls have no effect and cannot influence failure detection or cancellation. |
| TMP3104 | Warning | Heartbeat called unnecessarily | Short activities finish quickly; heartbeating them adds overhead without meaningfully improving failure detection. |
| TMP3105 | Warning | ActivityExecutionContext captured across an await | ActivityExecutionContext is async-local to the activity. Prefer accessing Current fresh, or store the specific values you need (logger, info, cancellation token) instead of holding the context itself. |
| TMP3106 | Warning | Non-SDK logger used in an activity | Console and other process-wide loggers bypass the activity's configured logging. Log through ActivityExecutionContext.Current.Logger. |
| TMP3107 | Warning | HttpClient call without a CancellationToken | HTTP calls should honor the activity's CancellationToken so a cancelled activity stops its network I/O immediately. |
| TMP3108 | Warning | HeartbeatTimeout much shorter than StartToCloseTimeout | A HeartbeatTimeout far shorter than StartToCloseTimeout causes the activity to be marked failed on the first missed heartbeat. |
| TMP3109 | Warning | Activity heartbeats in a loop but never checks the CancellationToken | Heartbeating without checking the cancellation token defeats cancellation: the activity keeps running after the heartbeat times out. |
| TMP3201 | Error | Invalid workflow entry method | A workflow entry method must be public, return Task, be declared in a [Workflow] class, and be the only [WorkflowRun] method. |
| TMP3202 | Error | Invalid activity declaration | The [Activity] attribute may only be applied to methods, and a method passed by typed lambda to ExecuteActivityAsync must be marked [Activity]. |
| TMP3203 | Warning | Activity method mutates instance state | Activities are not required to be stateless — DI-injected readonly fields are idiomatic. But writing to mutable instance fields or properties from an activity method races when a worker shares a single activity instance across concurrent executions. |
| TMP3204 | Error | Invalid workflow query method | A query handler must be synchronous and return a value: it must not be async and must not return void, Task, or Task<T>. |
| TMP3205 | Error | Invalid workflow signal method | A signal handler must return void or Task; returning Task<T> or a value is not allowed. |
| TMP3206 | Error | Workflow query mutates workflow state | Query handlers must not mutate workflow state; writing to instance fields or properties makes query results non-deterministic. |
| TMP3207 | Error | Workflow API called inside a query | Query handlers run synchronously and must not schedule workflow commands or mutate state; avoid calling Workflow APIs such as DelayAsync, WaitConditionAsync, or ExecuteActivityAsync from a query. |
| TMP3208 | Error | Invalid workflow update return type | An update handler must return a Task (or Task<T> for a result); returning void or a non-task value is invalid. |
| TMP3209 | Error | Continue-as-new invoked inside an update handler | Continue-as-new replaces the workflow execution and is only valid from the main workflow method. Raising it from an update handler is rejected at run time. |
| TMP3211 | Warning | Workflow message name is not a string literal | Query, signal, and update names are part of the workflow's public contract. A computed name makes the contract unstable and breaks clients that reference the literal name. |
| TMP3212 | Error | Client/worker types used from workflow code | Workflow code runs on the replay-deterministic workflow thread; referencing client or worker types pulls the worker process into the workflow and breaks determinism. |
| TMP3213 | Warning | StartWorkflowAsync called without an explicit workflow id | Without an explicit workflow id, Temporal generates one per call and a retry can start a duplicate workflow. Set workflowId for idempotent starts. |
| TMP3214 | Warning | Workflow and activity methods mixed in one class | Workflow and activity methods live on different execution threads and have different contracts; mixing them in one class invites accidental cross-thread access. |
| TMP3215 | Error | Update validator mutates state or blocks | Update validators run synchronously before the update is accepted and must not mutate workflow state or perform blocking work such as activities, sleeps, or other commands. |
| TMP3216 | Warning | Signal or update handler schedules workflow commands | Signal and update handlers are invoked inline during workflow execution; scheduling activities, child workflows, or delays from them keeps the workflow blocked. |
| TMP3217 | Warning | Workflow may complete while async handlers are pending | Completing a workflow while an async signal or update handler is still running leaves the handler unjournaled. Await Workflow.AllHandlersFinished before completing. |
| TMP3218 | Error | [WorkflowInit] and [WorkflowRun] parameter lists differ | The [WorkflowInit] constructor receives the arguments that start the workflow; it must accept exactly the parameters the [WorkflowRun] method declares. |
| TMP3219 | Error | [Workflow] class has a parameterized constructor without [WorkflowInit] | Workflow constructors are not dependency-injected. A parameterized constructor will be called with no arguments and fail unless a constructor is marked [WorkflowInit]. |
| TMP3301 | Error | Patch both applied and deprecated | A patch id that is both Patched and DeprecatePatch'd in the same workflow method is a leftover from a refactor and should be removed. |
| TMP3302 | Warning | Non-constant patch id | Workflow.Patched and Workflow.DeprecatePatch ids must be constant strings so version markers are stable across replays. |
| TMP3303 | Error | Patch id applied more than once | Applying the same patch id more than once in a workflow method is redundant and usually indicates a merge error. |
| TMP3305 | Warning | Patched call does not guard a behavior change | Workflow.Patched is meant to guard an incompatible behavior change; discarding its result means the patch has no effect. |
| TMP3307 | Warning | Patch fallback removed without deprecation | Once the old behavior is removed, the patch branch should be simplified and Workflow.DeprecatePatch called so the version marker is preserved for old replays. |

## Best practice

| ID | Default | Rule | Description |
|---|---|---|---|
| TMP4101 | Warning | Prefer a single object parameter | Passing many positional parameters to a workflow or activity couples the contract to argument order and makes it hard to evolve. Prefer a single object (DTO) parameter. |
| TMP4103 | Warning | Polling loop instead of Workflow.WaitConditionAsync | A loop that awaits a fixed delay to poll some state burns history and slows replay. Use Workflow.WaitConditionAsync or a signal-driven approach. |
| TMP4104 | off | CPU-heavy loop in workflow code | A loop in workflow code that never awaits blocks the single-threaded workflow and inflates replay time. Move CPU-heavy computation into an activity. |
| TMP4105 | Warning | Hard-coded task-queue name | Hard-coding a task queue name inline scatters the string across call sites and makes renaming error-prone. Extract it to a shared constant. |
| TMP4106 | Warning | Consecutive local activities | Running several local activities back-to-back in a workflow adds latency and history events. Combine the work into fewer activities, or batch the calls. |
| TMP4107 | Warning | Local activity performs blocking or network I/O | Local activities run on the worker's task queue and block a worker slot. Blocking or network I/O such as Task.Delay, sockets, or file I/O makes them long-running; use a regular activity instead. |
| TMP4201 | off | Workflow.NewGuid without a determinism comment | Workflow.NewGuid produces a deterministic, replay-stable GUID, not a cryptographically secure identifier. Document this so it is not mistaken for a security token. |
| TMP4202 | off | DeprecatePatch without an explanatory comment | Deprecating a patch changes how old workflows replay. Document the change so future readers understand the versioning history. |
| TMP4203 | off | Versioning change without a replay-tested comment | Behavior changes guarded by Workflow.Patched affect how old histories replay. Note that the change was replay-tested against existing workflow executions. |
