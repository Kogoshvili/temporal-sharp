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

    private static DiagnosticDescriptor Create(
        string id,
        string category,
        string title,
        string messageFormat,
        string description)
    {
        return new DiagnosticDescriptor(
            id: id,
            title: title,
            messageFormat: messageFormat,
            category: category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: description,
            helpLinkUri: "https://github.com/Kogoshvili/temporal-sharp/blob/main/RULES.md");
    }
}
