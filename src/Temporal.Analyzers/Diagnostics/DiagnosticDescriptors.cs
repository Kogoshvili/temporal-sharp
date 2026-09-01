using Microsoft.CodeAnalysis;

namespace Kogoshvili.Temporal.Analyzers.Diagnostics;

/// <summary>
/// All diagnostic descriptors for the Kogoshvili.Temporal analyzer, mapped to the
/// rule IDs documented in RULES.md.
/// </summary>
internal static class DiagnosticDescriptors
{
    private const string DeterminismCategory = "Determinism";
    private const string WorkflowStateCategory = "WorkflowState";
    private const string SdkMisuseCategory = "SdkMisuse";
    private const string BestPracticeCategory = "BestPractice";
    private const string TestingCategory = "Testing";

    internal static readonly DiagnosticDescriptor WallClockTime = Create(
        "TMP0101",
        DeterminismCategory,
        "Workflow code depends on wall-clock time",
        "'{0}' is non-deterministic in workflow code; use Workflow.UtcNow instead",
        "Workflow code is replayed by re-execution; reading wall-clock time during replay produces different results. Use Workflow.UtcNow.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor BlockOrSleep = Create(
        "TMP0111",
        DeterminismCategory,
        "Workflow code blocks the workflow thread",
        "'{0}' is non-deterministic in workflow code; use Workflow.DelayAsync for delays and 'await' instead of blocking waits",
        "Sleeping on wall-clock time or synchronously waiting on a task breaks replay determinism and deadlocks the single-threaded workflow runtime. Await asynchronously, and use Workflow.DelayAsync for delays.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor ConfigureAwaitFalse = Create(
        "TMP0113",
        DeterminismCategory,
        "Workflow code uses ConfigureAwait(false)",
        "'{0}' leaves the workflow synchronization context; omit ConfigureAwait or pass true",
        "ConfigureAwait(false) abandons the workflow's synchronization context, so continuations run on the default task scheduler rather than the deterministic workflow scheduler. Omit the call or pass true.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor FloatingTask = Create(
        "TMP0112",
        DeterminismCategory,
        "Workflow code discards an un-awaited task",
        "'{0}' returns a task that is not awaited; await it so its completion is journaled",
        "Fire-and-forget task calls in workflow code are not tracked by the deterministic scheduler, so their completion is not journaled and replays diverge. Await the task.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor NonDeterministicRandomness = Create(
        "TMP0121",
        DeterminismCategory,
        "Workflow code uses non-deterministic randomness",
        "'{0}' is non-deterministic in workflow code; use Workflow.Random or Workflow.NewGuid instead",
        "Random values generated in workflow code differ on replay. Use Workflow.Random or Workflow.NewGuid.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor IoOrEnvironmentAccess = Create(
        "TMP0131",
        DeterminismCategory,
        "Workflow code performs I/O or reads the environment",
        "'{0}' is non-deterministic in workflow code; move it into an activity instead",
        "I/O and environment access break replay determinism. Perform I/O in an activity.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor StaticStateMutation = Create(
        "TMP1101",
        WorkflowStateCategory,
        "Workflow code mutates static state",
        "Static member '{0}' is mutated from workflow code; shared mutable state breaks replay determinism and races across executions",
        "Workflow instances may be replayed or run concurrently; mutating static state produces different results across executions.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor ActivityMissingTimeout = Create(
        "TMP2101",
        SdkMisuseCategory,
        "Activity options missing required timeout",
        "Activity options set neither StartToCloseTimeout nor ScheduleToCloseTimeout; set at least one, or the activity is rejected at run time",
        "Temporal requires at least one of StartToCloseTimeout or ScheduleToCloseTimeout on ActivityOptions and LocalActivityOptions.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor StringTarget = Create(
        "TMP2111",
        SdkMisuseCategory,
        "Workflow target named by string",
        "Target '{0}' is named by string; pass a typed lambda instead so arguments can be checked statically",
        "String-named targets cannot be resolved statically and bypass compile-time type checking. This overload is legitimate for dynamic workflows, so the rule is opt-in.",
        isEnabledByDefault: false);

    internal static readonly DiagnosticDescriptor ContinueAsNewNotThrown = Create(
        "TMP2121",
        SdkMisuseCategory,
        "Continue-as-new exception is not thrown",
        "The ContinueAsNewException is created but not thrown; the workflow silently ends instead of continuing as new",
        "CreateContinueAsNewException returns an exception that must be thrown to trigger continue-as-new.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor NonReplayAwareLogger = Create(
        "TMP2131",
        SdkMisuseCategory,
        "Non-replay-aware logging in workflow code",
        "'{0}' logs during replay and double-logs; use Workflow.Logger instead",
        "Standard loggers write on every replay. Use Workflow.Logger, which suppresses output during replay.");

    internal static readonly DiagnosticDescriptor ConcurrentExecution = Create(
        "TMP0141",
        DeterminismCategory,
        "Workflow code starts concurrent work",
        "'{0}' starts concurrent work in workflow code; move the work into an activity instead",
        "Starting threads or parallel work in workflow code breaks replay determinism and has no Workflow.* replacement. Delegate the work to an activity.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor ConcurrentTaskRun = Create(
        "TMP0146",
        DeterminismCategory,
        "Workflow code starts work on the default task scheduler",
        "'{0}' uses the default task scheduler in workflow code; use Workflow.RunTaskAsync instead",
        "Task.Run schedules work on the thread-pool TaskScheduler rather than the deterministic workflow scheduler. Use Workflow.RunTaskAsync.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor BlockingPrimitive = Create(
        "TMP0142",
        DeterminismCategory,
        "Workflow code uses a blocking synchronization primitive",
        "'{0}' blocks workflow code; move the synchronization into an activity instead",
        "Blocking on locks, channels, or other synchronization primitives can deadlock the single-threaded workflow runtime and break determinism.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor BlockingSyncReplacement = Create(
        "TMP0147",
        DeterminismCategory,
        "Workflow code uses a blocking synchronization primitive with a deterministic replacement",
        "'{0}' blocks workflow code; use Temporalio.Workflows.Mutex or Temporalio.Workflows.Semaphore instead",
        "System.Threading.Mutex, Semaphore, and SemaphoreSlim block the single-threaded workflow runtime. Use the SDK's Temporalio.Workflows.Mutex / Temporalio.Workflows.Semaphore, which integrate with the deterministic scheduler.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor TaskScheduling = Create(
        "TMP0143",
        DeterminismCategory,
        "Workflow code uses raw task scheduling",
        "'{0}' runs on the non-deterministic task scheduler; use Workflow.WhenAnyAsync or CancellationTokenSource.Cancel instead",
        "The enumerable and typed-result overloads of Task.WhenAny<T>, and CancellationTokenSource.CancelAsync, schedule continuations on the default TaskScheduler rather than the deterministic workflow scheduler. Prefer Workflow.WhenAnyAsync and use .Cancel() instead of CancellationTokenSource.CancelAsync().",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor TaskWhenAll = Create(
        "TMP0148",
        DeterminismCategory,
        "Workflow code uses Task.WhenAll instead of Workflow.WhenAllAsync",
        "'{0}' is technically safe today, but use Workflow.WhenAllAsync for determinism clarity",
        "Task.WhenAll currently runs on the workflow scheduler, but the wrapper is the supported, forward-compatible API. Prefer Workflow.WhenAllAsync.",
        severity: DiagnosticSeverity.Info);

    internal static readonly DiagnosticDescriptor ManualTaskCoordination = Create(
        "TMP0144",
        DeterminismCategory,
        "Workflow code uses raw task coordination",
        "'{0}' is not owned by the deterministic workflow scheduler; use Workflow.WaitConditionAsync on a field instead",
        "TaskCompletionSource produces a Task whose completion and continuation timing are controlled by the default scheduler rather than the replay-deterministic workflow scheduler. Store a field and await it with Workflow.WaitConditionAsync.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor ReflectionInvocation = Create(
        "TMP0145",
        DeterminismCategory,
        "Workflow code uses reflection or dynamic invocation",
        "'{0}' is non-deterministic in workflow code; move it into an activity instead",
        "Reflection and dynamic invocation run arbitrary, non-journaled code on the workflow path, so side effects re-run on replay. Delegate this work to an activity.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor UnorderedEnumeration = Create(
        "TMP0151",
        DeterminismCategory,
        "Workflow code iterates a collection in non-deterministic order",
        "'{0}' may enumerate in non-deterministic order; sort the collection before iterating",
        "Dictionary and HashSet iteration order is not deterministic across runs and replays. Sort the collection first.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor CultureSensitiveParse = Create(
        "TMP0161",
        DeterminismCategory,
        "Workflow code parses or formats using the ambient culture",
        "'{0}' is culture-sensitive in workflow code; pass System.Globalization.CultureInfo.InvariantCulture",
        "Parsing or formatting numbers, dates, and times with the ambient culture diverges across workers and replays. Pass CultureInfo.InvariantCulture (or another explicit IFormatProvider).");

    internal static readonly DiagnosticDescriptor CryptoRandomness = Create(
        "TMP0122",
        DeterminismCategory,
        "Workflow code generates cryptographic randomness",
        "'{0}' generates cryptographic randomness in workflow code; move it into an activity",
        "Cryptographic RNG reads from OS entropy, which differs on replay. Move crypto-random generation into an activity, or use Workflow.Random for non-cryptographic randomness.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor Finalizer = Create(
        "TMP0171",
        DeterminismCategory,
        "Workflow type declares a finalizer",
        "'{0}' declares a finalizer; GC timing is non-deterministic in workflow code",
        "Finalizers run on GC timing, which is non-deterministic and can run during replay. Move cleanup to an activity or a deterministic shutdown path.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor TimerScheduling = Create(
        "TMP0172",
        DeterminismCategory,
        "Workflow code schedules a wall-clock timer",
        "'{0}' schedules work on wall-clock time; use Workflow.DelayAsync or move it into an activity",
        "System timers fire on wall-clock intervals and do not integrate with the deterministic workflow scheduler. Use Workflow.DelayAsync, or run timer-driven work in an activity.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor WeakReference = Create(
        "TMP0174",
        DeterminismCategory,
        "Workflow code uses a weak reference",
        "'{0}' depends on GC timing; weak references are non-deterministic in workflow code",
        "WeakReference and ConditionalWeakTable depend on GC timing, which is non-deterministic during replay. Avoid weak references in workflow code.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor ModuleSideEffect = Create(
        "TMP0177",
        DeterminismCategory,
        "Workflow module schedules workflow commands at load",
        "'{0}' runs at module load and schedules workflow commands; move it into a workflow method or activity",
        "Static constructors, static field initializers, and module initializers run at type/module load, before a workflow context exists and outside journaled history. Avoid scheduling workflow commands from them.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor NondeterministicControlFlow = Create(
        "TMP0175",
        DeterminismCategory,
        "Workflow control flow depends on time or randomness",
        "'{0}' branches or loops on a non-deterministic time or randomness source; use Workflow.UtcNow / Workflow.Random",
        "Control flow that depends on wall-clock time or non-deterministic randomness can diverge on replay. Use the deterministic SDK sources, or make the decision in an activity.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor WallClockComparison = Create(
        "TMP0104",
        DeterminismCategory,
        "Workflow time compared to a persisted value",
        "'{0}' compares workflow time to a persisted timestamp; use a workflow timer for deadlines",
        "Workflow.UtcNow returns the wall-clock time of the last workflow task, so it does not advance while the workflow is waiting and cannot test whether a real-world deadline has passed. Use a workflow timer, or persist the comparison time as workflow state.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor PollingLoop = Create(
        "TMP4103",
        BestPracticeCategory,
        "Polling loop instead of Workflow.WaitConditionAsync",
        "'{0}' polls with a delay; use Workflow.WaitConditionAsync for signals, or move external polling into an activity",
        "A loop that awaits a fixed delay burns history and slows replay. For local-state changes use Workflow.WaitConditionAsync; for polling external state, move the loop into an activity.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor ThreadStaticMutation = Create(
        "TMP1102",
        WorkflowStateCategory,
        "Workflow code mutates [ThreadStatic] state",
        "Static member '{0}' is [ThreadStatic] and is mutated from workflow code",
        "[ThreadStatic] state is per-thread and not deterministic during workflow replay.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor StaticPropertySetter = Create(
        "TMP1103",
        WorkflowStateCategory,
        "Workflow code sets a static property",
        "Static property '{0}' is set from workflow code",
        "Mutating static state from workflow code breaks replay determinism and races across executions.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor WaitConditionWithoutTimeout = Create(
        "TMP2103",
        SdkMisuseCategory,
        "WaitConditionAsync called without a timeout",
        "Workflow.WaitConditionAsync has no timeout; the workflow can wait forever and leak open executions",
        "Waiting on a condition without a timeout can leave the workflow blocked forever if the signal never arrives. Use the timeout overload and handle the returned bool.",
        isEnabledByDefault: false);

    internal static readonly DiagnosticDescriptor WaitConditionTimeoutIgnored = Create(
        "TMP2104",
        SdkMisuseCategory,
        "WaitConditionAsync timeout result ignored",
        "Workflow.WaitConditionAsync timeout result is ignored; the timeout path is never handled",
        "When the bool returned by the timeout overload is discarded, the timed-out branch is never handled and the workflow proceeds as if the condition were met. Check the result and handle the timeout path.");

    internal static readonly DiagnosticDescriptor NonSerializableType = Create(
        "TMP2141",
        SdkMisuseCategory,
        "Non-serializable type in workflow/activity signature",
        "Type '{0}' is not serializable across the workflow/activity boundary",
        "Workflow and activity arguments and return values are serialized; delegates, streams, channels, and async enumerables cannot round-trip.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor SensitiveArgument = Create(
        "TMP2151",
        SdkMisuseCategory,
        "Workflow/activity parameter may contain sensitive data",
        "'{0}' matches the sensitive-data pattern",
        "Workflow inputs are recorded in event history; avoid passing sensitive values directly.",
        isEnabledByDefault: false);

    internal static readonly DiagnosticDescriptor ActivityNeverHeartbeats = Create(
        "TMP3101",
        SdkMisuseCategory,
        "Long-running activity does not heartbeat",
        "Activity '{0}' contains a loop or multiple awaits but never calls ActivityExecutionContext.Heartbeat()",
        "Long-running activities should heartbeat so Temporal can detect failures and deliver cancellations.");

    internal static readonly DiagnosticDescriptor HeartbeatTimeoutWithoutHeartbeat = Create(
        "TMP3102",
        SdkMisuseCategory,
        "HeartbeatTimeout set but no heartbeat detected",
        "Activity '{0}' is invoked with HeartbeatTimeout set but no heartbeat call is detected",
        "HeartbeatTimeout requires the activity to record heartbeats; otherwise the activity will be considered failed. Detection recognizes Heartbeat() calls made directly by the activity or through helper methods it calls.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor HeartbeatWithoutTimeout = Create(
        "TMP3103",
        SdkMisuseCategory,
        "Heartbeat called without HeartbeatTimeout",
        "Activity '{0}' calls Heartbeat() but is invoked without a HeartbeatTimeout; set one so the heartbeat is effective",
        "Without a HeartbeatTimeout, heartbeats deliver no cancellation and enable no timeout-based failure detection. Set a HeartbeatTimeout so the heartbeat has an effect.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor UnnecessaryHeartbeat = Create(
        "TMP3104",
        SdkMisuseCategory,
        "Heartbeat called unnecessarily",
        "Activity '{0}' calls Heartbeat() but has no loop and at most one await; the heartbeat is unnecessary",
        "Short activities finish quickly; heartbeating them adds overhead without meaningfully improving failure detection.",
        isEnabledByDefault: false,
        severity: DiagnosticSeverity.Info);

    internal static readonly DiagnosticDescriptor InvalidWorkflowRun = Create(
        "TMP3201",
        SdkMisuseCategory,
        "Invalid workflow entry method",
        "Invalid [WorkflowRun] method: {0}",
        "A workflow entry method must be public, return Task, be declared in a [Workflow] class, and be the only [WorkflowRun] method.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor StopwatchUsage = Create(
        "TMP0102",
        DeterminismCategory,
        "Workflow code measures elapsed wall-clock time",
        "'{0}' measures wall-clock time in workflow code; use Workflow.UtcNow instead",
        "A Stopwatch reads elapsed wall-clock time, which differs on replay. Use Workflow.UtcNow to read deterministic time.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor StaticCollectionMutation = Create(
        "TMP1104",
        WorkflowStateCategory,
        "Workflow code mutates a static collection",
        "Static collection '{0}' is mutated from workflow code; shared mutable state breaks replay determinism and races across executions",
        "Workflow instances may be replayed or run concurrently; mutating a static collection produces different results across executions.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor StaticMethodMutation = Create(
        "TMP1105",
        WorkflowStateCategory,
        "Workflow code mutates static state via a method call",
        "Static member '{0}' is mutated via a method call from workflow code; shared mutable state breaks replay determinism and races across executions",
        "Calling a mutating method on a shared static reference changes state visible to every workflow execution. Keep workflow state instance-local.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor AmbientState = Create(
        "TMP1106",
        WorkflowStateCategory,
        "Workflow code uses ambient AsyncLocal/ThreadLocal state",
        "'{0}' stores ambient state that is not deterministic during workflow replay; pass state explicitly instead",
        "AsyncLocal/ThreadLocal state lives in ExecutionContext/thread storage and is not re-established when a workflow task resumes after replay, so it is not deterministic replay state. Thread state explicitly through method parameters and fields.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor LossyNumber = Create(
        "TMP2171",
        SdkMisuseCategory,
        "Lossy-number parameter in workflow/activity signature",
        "Parameter '{0}' has type '{1}'; the JSON DataConverter may lose precision, use a concrete type instead",
        "object/dynamic values decode to JsonElement, and large integers may lose precision. Declare a concrete parameter type.",
        isEnabledByDefault: false);

    internal static readonly DiagnosticDescriptor InvalidActivity = Create(
        "TMP3202",
        SdkMisuseCategory,
        "Invalid activity declaration",
        "Invalid [Activity]: {0}",
        "The [Activity] attribute may only be applied to methods, and a method passed by typed lambda to ExecuteActivityAsync must be marked [Activity].",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor ActivityInstanceState = Create(
        "TMP3203",
        SdkMisuseCategory,
        "Activity method mutates instance state",
        "Activity method '{0}' writes to instance member '{1}'; mutable instance state races across concurrent executions",
        "Activities are not required to be stateless — DI-injected readonly fields are idiomatic. But writing to mutable instance fields or properties from an activity method races when a worker shares a single activity instance across concurrent executions.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor InvalidQuery = Create(
        "TMP3204",
        SdkMisuseCategory,
        "Invalid workflow query method",
        "Invalid [WorkflowQuery] method: {0}",
        "A query handler must be synchronous and return a value: it must not be async and must not return void, Task, or Task<T>.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor InvalidSignal = Create(
        "TMP3205",
        SdkMisuseCategory,
        "Invalid workflow signal method",
        "Invalid [WorkflowSignal] method: {0}",
        "A signal handler must return Task; returning void, Task<T>, or a value is not allowed.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor QueryMutation = Create(
        "TMP3206",
        SdkMisuseCategory,
        "Workflow query mutates workflow state",
        "Query method '{0}' writes to instance member '{1}'; queries must be read-only",
        "Query handlers must not mutate workflow state; writing to instance fields or properties makes query results non-deterministic.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor WorkflowApiInQuery = Create(
        "TMP3207",
        SdkMisuseCategory,
        "Workflow API called inside a query",
        "'{0}' is called inside a [WorkflowQuery]; queries must be synchronous and read-only",
        "Query handlers run synchronously and must not schedule workflow commands or mutate state; avoid calling Workflow APIs such as DelayAsync, WaitConditionAsync, or ExecuteActivityAsync from a query.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor PatchLeftover = Create(
        "TMP3301",
        SdkMisuseCategory,
        "Patch both applied and deprecated",
        "patch '{0}' is both Patched and DeprecatePatch'd in the same workflow",
        "A patch id that is both Patched and DeprecatePatch'd in the same workflow method is a leftover from a refactor and should be removed.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor NonConstantPatchId = Create(
        "TMP3302",
        SdkMisuseCategory,
        "Non-constant patch id",
        "{0} id must be a constant string",
        "Workflow.Patched and Workflow.DeprecatePatch ids must be constant strings so version markers are stable across replays.");

    internal static readonly DiagnosticDescriptor SearchAttributeNotUpserted = Create(
        "TMP2161",
        SdkMisuseCategory,
        "Search attribute is never upserted",
        "Workflow input field '{0}' maps to search attribute '{1}' but is never upserted via Workflow.UpsertTypedSearchAttributes",
        "A search attribute set at workflow start is only indexed then. Upsert it with Workflow.UpsertTypedSearchAttributes when its value changes.",
        isEnabledByDefault: false);

    internal static readonly DiagnosticDescriptor InvalidWorkflowUpdate = Create(
        "TMP3208",
        SdkMisuseCategory,
        "Invalid workflow update return type",
        "Invalid [WorkflowUpdate] method: {0}",
        "An update handler must return a Task (or Task<T> for a result); returning void or a non-task value is invalid.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor ContinueAsNewInUpdate = Create(
        "TMP3209",
        SdkMisuseCategory,
        "Continue-as-new invoked inside an update or signal handler",
        "'{0}' is invoked inside a [WorkflowUpdate]/[WorkflowSignal] handler; continue-as-new must be raised from the main workflow method",
        "Continue-as-new replaces the workflow execution and is only valid from the main workflow method. Raising it from an update or signal handler is unsupported and can interrupt in-flight work.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor ClientOrWorkerTypeInWorkflow = Create(
        "TMP3212",
        SdkMisuseCategory,
        "Client/worker types used from workflow code",
        "'{0}' is a Temporal client or worker type and must not be referenced from workflow code",
        "Workflow code runs on the replay-deterministic workflow thread; referencing client or worker types pulls the worker process into the workflow and breaks determinism.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor StandaloneActivityInWorkflow = Create(
        "TMP3213",
        SdkMisuseCategory,
        "Standalone activity API called from workflow code",
        "'{0}' is a standalone-activity client API; use Workflow.ExecuteActivityAsync inside a workflow",
        "TemporalClient.ExecuteActivityAsync and StartActivityAsync run an activity independently of a workflow. Inside workflow code, invoke activities with Workflow.ExecuteActivityAsync instead.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor MixedWorkflowAndActivity = Create(
        "TMP3214",
        SdkMisuseCategory,
        "Workflow and activity methods mixed in one class",
        "'{0}' mixes workflow and activity methods; split them into separate classes",
        "Workflow and activity methods live on different execution threads and have different contracts; mixing them in one class invites accidental cross-thread access.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor UpdateValidatorSideEffect = Create(
        "TMP3215",
        SdkMisuseCategory,
        "Update validator mutates state or blocks",
        "Update validator '{0}' {1}; validators must be pure and non-blocking",
        "Update validators run synchronously before the update is accepted and must not mutate workflow state or perform blocking work such as activities, sleeps, or other commands.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor HandlerSchedulesWork = Create(
        "TMP3216",
        SdkMisuseCategory,
        "Signal handler schedules workflow commands",
        "'{0}' is called inside a signal handler; signal handlers should return quickly without scheduling commands",
        "Signal handlers are invoked inline during workflow execution; scheduling activities, child workflows, or delays from them can keep the workflow blocked. Prefer setting state in the handler and doing the work in the main workflow method.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor CompleteWithPendingHandlers = Create(
        "TMP3217",
        SdkMisuseCategory,
        "Workflow may complete while async handlers are pending",
        "Workflow type '{0}' declares async handlers but never awaits Workflow.AllHandlersFinished",
        "Completing a workflow while an async signal or update handler is still running leaves the handler unjournaled. Await Workflow.AllHandlersFinished before completing.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor WorkflowInitMismatch = Create(
        "TMP3218",
        SdkMisuseCategory,
        "[WorkflowInit] and [WorkflowRun] parameter lists differ",
        "[WorkflowInit] constructor and [WorkflowRun] method '{0}' have mismatched parameter lists",
        "The [WorkflowInit] constructor receives the arguments that start the workflow; it must accept exactly the parameters the [WorkflowRun] method declares.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor WorkflowParameterizedCtor = Create(
        "TMP3219",
        SdkMisuseCategory,
        "[Workflow] class has no instantiable constructor without [WorkflowInit]",
        "[Workflow] class '{0}' has no parameterless constructor; mark a constructor with [WorkflowInit] to receive workflow arguments",
        "A workflow must have a parameterless constructor unless one is marked [WorkflowInit]; otherwise the worker cannot instantiate it and throws at startup.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor WorkflowNonInitParameterizedCtor = Create(
        "TMP3220",
        SdkMisuseCategory,
        "Parameterized constructor without [WorkflowInit] in a [Workflow] class",
        "[Workflow] class '{0}' has a constructor that is neither parameterless nor marked [WorkflowInit]; the worker never invokes it",
        "The worker only ever calls the parameterless constructor or the one marked [WorkflowInit]; any other parameterized constructor is dead code — typically a workaround for injecting ambient state such as singletons or configuration. Receive workflow arguments via [WorkflowInit] instead, or remove the constructor.");

    internal static readonly DiagnosticDescriptor WorkflowConstructorCommand = Create(
        "TMP3210",
        SdkMisuseCategory,
        "Workflow constructor schedules a workflow command",
        "'Workflow.{0}' is called from a workflow constructor; constructors cannot block or schedule commands",
        "A workflow constructor (including [WorkflowInit]) runs before the workflow context is established and cannot schedule activities, timers, or other commands. Move the work into the workflow method or an activity.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor SwallowedContinueAsNew = Create(
        "TMP2123",
        SdkMisuseCategory,
        "Continue-as-new is swallowed",
        "catch block swallows a ContinueAsNewException; rethrow it so the workflow continues as new",
        "A broad catch that swallows a ContinueAsNewException silently ends the workflow instead of continuing as new. Rethrow the exception so continue-as-new is triggered.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor CleanupNotNonCancellable = Create(
        "TMP2124",
        SdkMisuseCategory,
        "Cleanup after cancellation is not in a non-cancellable scope",
        "cleanup awaits a task outside a non-cancellable scope; pass CancellationToken.None (or a token from a detached CancellationTokenSource) to the cleanup work",
        "Cleanup that runs after cancellation should not itself be cancelled; pass CancellationToken.None (or a token from a detached CancellationTokenSource) to the cleanup work so it always completes.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor ContinueAsNewWithoutState = Create(
        "TMP2122",
        SdkMisuseCategory,
        "Continue-as-new without passing current workflow state",
        "Continue-as-new does not pass the current workflow state; pass it via the new run's arguments",
        "Continue-as-new starts a fresh execution; any state needed by the new run must be passed as arguments or it is lost.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor LongRunningLoopWithoutContinueAsNew = Create(
        "TMP2125",
        SdkMisuseCategory,
        "Unbounded loop without a continue-as-new check",
        "Long-running loop never checks Workflow.ContinueAsNewSuggested",
        "An unbounded loop that never checks Workflow.ContinueAsNewSuggested grows the workflow history until it hits the size limit. Break the loop and continue as new.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor ThrowsBaseException = Create(
        "TMP2132",
        SdkMisuseCategory,
        "Non-failure exception thrown from workflow code",
        "Throwing an exception that will not fail the workflow; throw a Temporalio.Exceptions.FailureException such as ApplicationFailureException instead",
        "By default a workflow only fails on a FailureException or cancellation; other exception types retry the task forever. Throw ApplicationFailureException, or configure WorkflowFailureExceptionTypes for the intended exception types.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor ActivityThrowsBaseException = Create(
        "TMP2134",
        SdkMisuseCategory,
        "Base exception thrown from an activity",
        "Activity throws a base exception; prefer Temporalio.Exceptions.ApplicationFailureException for a typed failure",
        "Any exception thrown from an activity is converted to an ApplicationFailure. Prefer ApplicationFailureException or a domain exception so the retry policy (for example, NonRetryableErrorTypes) can distinguish it.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor WorkflowNonRetryableApplicationFailure = Create(
        "TMP2135",
        SdkMisuseCategory,
        "nonRetryable set on an ApplicationFailureException thrown from a workflow",
        "Do not set nonRetryable: true on ApplicationFailureException thrown from workflow code",
        "nonRetryable only affects activity retry behavior. Setting it on an ApplicationFailureException thrown from workflow code is misleading because a workflow that fails with an ApplicationFailureException is not retried.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor AssertInWorkflow = Create(
        "TMP2133",
        SdkMisuseCategory,
        "Debug/Trace assert in workflow code",
        "'{0}' asserts in workflow code outside tests; remove the assertion",
        "Debug.Assert is compiled out in release, and Trace.Assert performs non-deterministic output in release builds; neither has a place in production workflow code.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor InternalTemporalNamespace = Create(
        "TMP2146",
        SdkMisuseCategory,
        "Use of internal Temporal namespace",
        "Use of internal Temporal namespace '{0}'",
        "Temporalio.Bridge is not part of the public API and can change without notice.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor UnsafeNamespaceReference = Create(
        "TMP2147",
        SdkMisuseCategory,
        "Unsafe namespace imported in workflow code",
        "Namespace '{0}' is configured as unsafe for workflow code",
        "Importing namespaces that provide I/O, networking, or other non-deterministic APIs into workflow code invites replay bugs. Configure kogoshvili.temporal.unsafe_namespaces with a list of namespace prefixes that workflow code must not import.",
        isEnabledByDefault: false);

    internal static readonly DiagnosticDescriptor WorkflowUnsafeUsage = Create(
        "TMP2148",
        SdkMisuseCategory,
        "Workflow.Unsafe used in workflow code",
        "'{0}' is from Workflow.Unsafe and should not be used in workflow code in most cases",
        "Workflow.Unsafe (for example IsReplaying) should not be used in most cases: branching on replay status breaks determinism. For logging use Workflow.Logger and for metrics use Workflow.Metrics; both are replay-aware. If you know what you are doing and why, suppress this diagnostic with #pragma warning disable TMP2148.");

    internal static readonly DiagnosticDescriptor BigIntegerInPayload = Create(
        "TMP2142",
        SdkMisuseCategory,
        "BigInteger in a payload without a converter",
        "Type '{0}' is not serializable without a custom payload converter",
        "System.Numerics.BigInteger is not supported by the default JSON payload converter. Register a custom converter or use a supported type.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor ExceptionInPayload = Create(
        "TMP2143",
        SdkMisuseCategory,
        "Exception used as a payload",
        "Type '{0}' (an Exception) is passed across the workflow/activity boundary; prefer ApplicationFailure or error codes",
        "Serializing an Exception as a payload is lossy and couples the workflow contract to the .NET exception hierarchy. Use ApplicationFailure or a plain error model.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor LargeInlinePayload = Create(
        "TMP2144",
        SdkMisuseCategory,
        "Oversized inline payload",
        "Inline payload is unusually large; move it out of workflow code",
        "Very large inline literals and collection initializers bloat source and, when serialized, event history. Move them to a field, an activity, or external storage.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor NestedLossyNumber = Create(
        "TMP2172",
        SdkMisuseCategory,
        "Lossy-number member in a workflow/activity payload type",
        "Member '{0}' has type '{1}'; the JSON DataConverter may lose precision, use a concrete type instead",
        "object/dynamic/JsonElement members decode to JsonElement and may lose precision. Declare a concrete member type on the payload DTO.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor NonSdkActivityLog = Create(
        "TMP3106",
        SdkMisuseCategory,
        "Non-SDK logger used in an activity",
        "'{0}' writes to a non-SDK logger; use ActivityExecutionContext.Current.Logger instead",
        "Console and other process-wide loggers bypass the activity's configured logging. Log through ActivityExecutionContext.Current.Logger.",
        severity: DiagnosticSeverity.Info);

    internal static readonly DiagnosticDescriptor HttpClientWithoutCancellation = Create(
        "TMP3107",
        SdkMisuseCategory,
        "HttpClient call without a CancellationToken",
        "'{0}' is called without a CancellationToken; pass one so cancellation can propagate",
        "HTTP calls should honor the activity's CancellationToken so a cancelled activity stops its network I/O immediately.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor HeartbeatTimeoutMismatch = Create(
        "TMP3108",
        SdkMisuseCategory,
        "HeartbeatTimeout much shorter than StartToCloseTimeout",
        "HeartbeatTimeout is much shorter than StartToCloseTimeout; the activity may be considered failed long before it can complete",
        "A HeartbeatTimeout far shorter than StartToCloseTimeout causes the activity to be marked failed on the first missed heartbeat.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor HeartbeatWithoutCancellationCheck = Create(
        "TMP3109",
        SdkMisuseCategory,
        "Activity heartbeats in a loop but never checks the CancellationToken",
        "Activity '{0}' heartbeats in a loop but never checks the CancellationToken",
        "Heartbeating without checking the cancellation token defeats cancellation: the activity keeps running after the heartbeat times out.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor UpsertInLoop = Create(
        "TMP2162",
        SdkMisuseCategory,
        "Search attributes upserted inside a loop",
        "Workflow.UpsertTypedSearchAttributes is called inside a loop",
        "Upserting search attributes on every loop iteration writes a command to history each time. Upsert once after the loop, or only when the value changes.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor SearchAttributeUnsetShape = Create(
        "TMP2163",
        SdkMisuseCategory,
        "Search attribute removal uses the wrong shape",
        "Search-attribute removal should use ValueUnset(), not ValueSet(null)",
        "Use SearchAttributeKey<T>.ValueUnset() to remove an attribute; ValueSet(null) is non-idiomatic and corrupts the local attribute collection.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor DuplicatePatchId = Create(
        "TMP3303",
        SdkMisuseCategory,
        "Patch id applied more than once",
        "patch id '{0}' is Patched more than once in the same workflow method",
        "Applying the same patch id more than once in a workflow method is redundant and usually indicates a merge error.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor PatchWithoutGuard = Create(
        "TMP3305",
        SdkMisuseCategory,
        "Patched call does not guard a behavior change",
        "Workflow.Patched result is discarded; the patch does not guard a behavior change",
        "Workflow.Patched is meant to guard an incompatible behavior change; discarding its result means no behavior is actually changed (though the marker is still recorded).",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor MultipleParameters = Create(
        "TMP4101",
        BestPracticeCategory,
        "Prefer a single object parameter",
        "'{0}' takes {1} parameters; prefer a single object parameter",
        "Passing many positional parameters to a workflow or activity couples the contract to argument order and makes it hard to evolve. Prefer a single object (DTO) parameter.",
        severity: DiagnosticSeverity.Info);

    internal static readonly DiagnosticDescriptor HeavyCpuLoop = Create(
        "TMP4104",
        BestPracticeCategory,
        "CPU-heavy loop in workflow code",
        "'{0}' is a loop with no await; move CPU-heavy work into an activity",
        "A loop in workflow code that never awaits blocks the single-threaded workflow and inflates replay time. Move CPU-heavy computation into an activity.",
        isEnabledByDefault: false);

    internal static readonly DiagnosticDescriptor HardcodedTaskQueue = Create(
        "TMP4105",
        BestPracticeCategory,
        "Hard-coded task-queue name",
        "Task queue '{0}' is hard-coded; use a shared constant",
        "Hard-coding a task queue name inline scatters the string across call sites and makes renaming error-prone. Extract it to a shared constant.",
        severity: DiagnosticSeverity.Info);

    internal static readonly DiagnosticDescriptor ConsecutiveLocalActivities = Create(
        "TMP4106",
        BestPracticeCategory,
        "Consecutive local activities",
        "Consecutive ExecuteLocalActivityAsync calls with no intervening workflow command",
        "Local-activity completions are only persisted when the workflow task completes; running several back-to-back with no yield risks losing work if the worker crashes mid-sequence. Combine the work into fewer activities, or batch the calls.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor LocalActivityBlockingIo = Create(
        "TMP4107",
        BestPracticeCategory,
        "Local activity performs blocking or long-running I/O",
        "'{0}' runs in a local activity; local activities must be short and lightweight",
        "Local activities run on the worker's task queue and must complete quickly. Blocking I/O or long-running work such as Task.Delay, sockets, or file I/O makes them long-running; use a regular activity instead.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor BusyPollingVersionFlag = Create(
        "TMP4108",
        BestPracticeCategory,
        "Busy-polling a worker-version flag on a timer",
        "Loop polls Workflow.TargetWorkerDeploymentVersionChanged on a timer; check it at a workflow task boundary instead",
        "Workflow.TargetWorkerDeploymentVersionChanged only refreshes after a workflow task completes. Check it at a natural workflow task boundary; use a timer only to wake an otherwise-idle workflow.",
        severity: DiagnosticSeverity.Warning);

    internal static readonly DiagnosticDescriptor MissingReplayTest = Create(
        "TMP5001",
        TestingCategory,
        "No replay test for the workflow",
        "[Workflow] type '{0}' has no WorkflowReplayer-based replay test",
        "Workflows are replayed by re-execution, so a non-deterministic change silently breaks existing histories. Add a WorkflowReplayer-based replay test that replays captured history to catch non-determinism. The rule is opt-in and requires the workflow and its replay test to be visible together.",
        isEnabledByDefault: false);

    internal static readonly DiagnosticDescriptor EnvironmentNotTornDown = Create(
        "TMP5002",
        TestingCategory,
        "Test workflow environment not torn down",
        "Test workflow environment '{0}' is not disposed; use 'await using' or call DisposeAsync",
        "A WorkflowEnvironment starts a local Temporal server that must be shut down when the test ends. Scope it with 'await using' or call ShutdownAsync/DisposeAsync in teardown. The rule is opt-in.",
        isEnabledByDefault: false);

    internal static readonly DiagnosticDescriptor WorkerNotScoped = Create(
        "TMP5003",
        TestingCategory,
        "Worker lifecycle not scoped via ExecuteAsync",
        "Test workflow environment is used without worker.ExecuteAsync(...) scoping",
        "Workflows only run while a worker is executing; without worker.ExecuteAsync(...) the worker never processes the workflow. Scope the worker run with ExecuteAsync (or RunUntilAsync) and a time-skipping environment. The rule is opt-in.",
        isEnabledByDefault: false);

    private static DiagnosticDescriptor Create(
        string id,
        string category,
        string title,
        string messageFormat,
        string description,
        bool isEnabledByDefault = true,
        DiagnosticSeverity severity = DiagnosticSeverity.Warning)
    {
        return new DiagnosticDescriptor(
            id: id,
            title: title,
            messageFormat: messageFormat,
            category: category,
            defaultSeverity: severity,
            isEnabledByDefault: isEnabledByDefault,
            description: description,
            helpLinkUri: "https://github.com/Kogoshvili/temporal-sharp/blob/main/RULES.md");
    }
}
