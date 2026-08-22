using Microsoft.CodeAnalysis;

namespace TemporalSharp.Analyzers.Diagnostics;

/// <summary>
/// All diagnostic descriptors for the TemporalSharp analyzer, mapped to the
/// rule IDs documented in RULES.md.
/// </summary>
internal static class DiagnosticDescriptors
{
    private const string DeterminismCategory = "TemporalSharp.Determinism";
    private const string WorkflowStateCategory = "TemporalSharp.WorkflowState";
    private const string SdkMisuseCategory = "TemporalSharp.SdkMisuse";

    internal static readonly DiagnosticDescriptor WallClockTime = Create(
        "TMP0101",
        DeterminismCategory,
        "Workflow code depends on wall-clock time",
        "'{0}' is non-deterministic in workflow code; use Workflow.UtcNow instead",
        "Workflow code is replayed by re-execution; reading wall-clock time during replay produces different results. Use Workflow.UtcNow.");

    internal static readonly DiagnosticDescriptor BlockOrSleep = Create(
        "TMP0111",
        DeterminismCategory,
        "Workflow code blocks on wall-clock time",
        "'{0}' is non-deterministic in workflow code; use Workflow.DelayAsync instead",
        "Sleeping or blocking on wall-clock time breaks replay determinism. Use Workflow.DelayAsync.");

    internal static readonly DiagnosticDescriptor NonDeterministicRandomness = Create(
        "TMP0121",
        DeterminismCategory,
        "Workflow code uses non-deterministic randomness",
        "'{0}' is non-deterministic in workflow code; use Workflow.Random or Workflow.NewGuid instead",
        "Random values generated in workflow code differ on replay. Use Workflow.Random or Workflow.NewGuid.");

    internal static readonly DiagnosticDescriptor IoOrEnvironmentAccess = Create(
        "TMP0131",
        DeterminismCategory,
        "Workflow code performs I/O or reads the environment",
        "'{0}' is non-deterministic in workflow code; move it into an activity instead",
        "I/O and environment access break replay determinism. Perform I/O in an activity.");

    internal static readonly DiagnosticDescriptor StaticStateMutation = Create(
        "TMP1101",
        WorkflowStateCategory,
        "Workflow code mutates static state",
        "Static member '{0}' is mutated from workflow code; shared mutable state breaks replay determinism and races across executions",
        "Workflow instances may be replayed or run concurrently; mutating static state produces different results across executions.");

    internal static readonly DiagnosticDescriptor ActivityMissingTimeout = Create(
        "TMP2101",
        SdkMisuseCategory,
        "Activity options missing required timeout",
        "Activity options set no required timeout; set StartToCloseTimeout or ScheduleToCloseTimeout, or the activity is rejected at run time",
        "Temporal requires StartToCloseTimeout or ScheduleToCloseTimeout on ActivityOptions and LocalActivityOptions.");

    internal static readonly DiagnosticDescriptor StringTarget = Create(
        "TMP2111",
        SdkMisuseCategory,
        "Workflow target named by string",
        "Target '{0}' is named by string; pass a typed lambda instead so arguments can be checked statically",
        "String-named targets cannot be resolved statically and bypass compile-time type checking.");

    internal static readonly DiagnosticDescriptor ContinueAsNewNotThrown = Create(
        "TMP2121",
        SdkMisuseCategory,
        "Continue-as-new exception is not thrown",
        "The ContinueAsNewException is created but not thrown; the workflow silently ends instead of continuing as new",
        "CreateContinueAsNewException returns an exception that must be thrown to trigger continue-as-new.");

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
        "'{0}' starts concurrent work in workflow code; use Workflow.ExecuteActivityAsync or Workflow.DelayAsync instead",
        "Starting threads or tasks in workflow code breaks replay determinism. Delegate concurrent work to activities.");

    internal static readonly DiagnosticDescriptor BlockingPrimitive = Create(
        "TMP0142",
        DeterminismCategory,
        "Workflow code uses a blocking synchronization primitive",
        "'{0}' blocks workflow code; use an async alternative instead",
        "Blocking on locks, channels, or other synchronization primitives can deadlock the single-threaded workflow runtime and break determinism.");

    internal static readonly DiagnosticDescriptor UnorderedEnumeration = Create(
        "TMP0151",
        DeterminismCategory,
        "Workflow code iterates a collection in non-deterministic order",
        "'{0}' may enumerate in non-deterministic order; sort the collection before iterating",
        "Dictionary and HashSet iteration order is not deterministic across runs and replays. Sort the collection first.");

    internal static readonly DiagnosticDescriptor ThreadStaticMutation = Create(
        "TMP1102",
        WorkflowStateCategory,
        "Workflow code mutates [ThreadStatic] state",
        "Static member '{0}' is [ThreadStatic] and is mutated from workflow code",
        "[ThreadStatic] state is per-thread and not deterministic during workflow replay.");

    internal static readonly DiagnosticDescriptor StaticPropertySetter = Create(
        "TMP1103",
        WorkflowStateCategory,
        "Workflow code sets a static property",
        "Static property '{0}' is set from workflow code",
        "Mutating static state from workflow code breaks replay determinism and races across executions.");

    internal static readonly DiagnosticDescriptor MissingStartToCloseTimeout = Create(
        "TMP2102",
        SdkMisuseCategory,
        "ScheduleToCloseTimeout set without StartToCloseTimeout",
        "ScheduleToCloseTimeout is set but StartToCloseTimeout is not",
        "When both are relevant, StartToCloseTimeout should be set so the activity cannot run for longer than expected.",
        isEnabledByDefault: false);

    internal static readonly DiagnosticDescriptor NonSerializableType = Create(
        "TMP2141",
        SdkMisuseCategory,
        "Non-serializable type in workflow/activity signature",
        "Type '{0}' is not serializable across the workflow/activity boundary",
        "Workflow and activity arguments and return values are serialized; delegates, streams, channels, and async enumerables cannot round-trip.");

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
        "HeartbeatTimeout requires the activity to record heartbeats; otherwise the activity will be considered failed.");

    internal static readonly DiagnosticDescriptor InvalidWorkflowRun = Create(
        "TMP3201",
        SdkMisuseCategory,
        "Invalid workflow entry method",
        "Invalid [WorkflowRun] method: {0}",
        "A workflow entry method must be public, return Task, be declared in a [Workflow] class, and be the only [WorkflowRun] method.");

    internal static readonly DiagnosticDescriptor StopwatchUsage = Create(
        "TMP0102",
        DeterminismCategory,
        "Workflow code measures elapsed wall-clock time",
        "'{0}' measures wall-clock time in workflow code; use Workflow.UtcNow instead",
        "A Stopwatch reads elapsed wall-clock time, which differs on replay. Use Workflow.UtcNow to read deterministic time.");

    internal static readonly DiagnosticDescriptor StaticCollectionMutation = Create(
        "TMP1104",
        WorkflowStateCategory,
        "Workflow code mutates a static collection",
        "Static collection '{0}' is mutated from workflow code; shared mutable state breaks replay determinism and races across executions",
        "Workflow instances may be replayed or run concurrently; mutating a static collection produces different results across executions.");

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
        "Invalid activity method",
        "Invalid [Activity]: {0}",
        "An activity method must be public, return Task or Task<T>, and be marked [Activity] when targeted by ExecuteActivityAsync.");

    internal static readonly DiagnosticDescriptor VersioningMisuse = Create(
        "TMP3301",
        SdkMisuseCategory,
        "Workflow versioning (patch) misuse",
        "Versioning misuse: {0}",
        "Workflow.Patched and Workflow.DeprecatePatch ids must be constant strings and a patch must not be both patched and deprecated.");

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
        bool isEnabledByDefault = true)
    {
        return new DiagnosticDescriptor(
            id: id,
            title: title,
            messageFormat: messageFormat,
            category: category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: isEnabledByDefault,
            description: description,
            helpLinkUri: "https://github.com/Kogoshvili/temporal-sharp/blob/main/RULES.md");
    }
}
