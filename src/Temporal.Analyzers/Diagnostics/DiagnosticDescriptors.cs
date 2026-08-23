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
        "'{0}' blocks workflow code; use 'await' (or Workflow.DelayAsync for time-based waits) instead",
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
        "'{0}' returns a task that is neither awaited nor assigned; await it or discard it explicitly with '_ ='",
        "Fire-and-forget task calls in workflow code are not tracked by the deterministic scheduler, so their completion is not journaled and replays diverge. Await the task, assign it, or discard it explicitly.",
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
        "Task.Run and TaskFactory.StartNew schedule work on the default task scheduler rather than the deterministic workflow scheduler. Use Workflow.RunTaskAsync.",
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
        "'{0}' runs on the non-deterministic task scheduler; use Workflow.WhenAllAsync / Workflow.WhenAnyAsync instead",
        "Raw Task combinators (WhenAll/WhenAny/ContinueWith) schedule continuations on the default TaskScheduler rather than the deterministic workflow scheduler. Prefer Workflow.WhenAllAsync / Workflow.WhenAnyAsync, and use .Cancel() instead of CancellationTokenSource.CancelAsync().");

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
        "Workflow.WaitConditionAsync timeout result is ignored; the timeout provides no protection",
        "When the bool returned by the timeout overload is discarded, the timeout has no effect and the workflow proceeds as if the condition were met. Check the result and handle the timeout path.");

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
        "Workflow/activity parameter or property may contain sensitive data",
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
        "HeartbeatTimeout set but activity never heartbeats",
        "Activity '{0}' is invoked with HeartbeatTimeout set but never calls ActivityExecutionContext.Heartbeat()",
        "HeartbeatTimeout requires the activity to record heartbeats; otherwise the activity will be considered failed.",
        severity: DiagnosticSeverity.Error);

    internal static readonly DiagnosticDescriptor HeartbeatWithoutTimeout = Create(
        "TMP3103",
        SdkMisuseCategory,
        "Heartbeat called without HeartbeatTimeout",
        "Activity '{0}' calls Heartbeat() but is invoked without a HeartbeatTimeout; set one so the heartbeat takes effect",
        "Without a HeartbeatTimeout, heartbeat calls have no effect and cannot influence failure detection or cancellation.");

    internal static readonly DiagnosticDescriptor UnnecessaryHeartbeat = Create(
        "TMP3104",
        SdkMisuseCategory,
        "Heartbeat called unnecessarily",
        "Activity '{0}' calls Heartbeat() but has no loop and at most one await; the heartbeat is unnecessary",
        "Short activities finish quickly; heartbeating them adds overhead without meaningfully improving failure detection.");

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
        "AsyncLocal/ThreadLocal state lives in ExecutionContext/thread storage and flows across awaits in scheduler-dependent ways, so it is not deterministic replay state. Thread state explicitly through method parameters and fields.",
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
        "A signal handler must return void or Task; returning Task<T> or a value is not allowed.",
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
