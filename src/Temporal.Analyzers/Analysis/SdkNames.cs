using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Kogoshvili.Temporal.Analyzers.Analysis;

/// <summary>
/// Fully-qualified names of Temporal .NET SDK types and members used for
/// matching, without referencing the SDK assembly.
/// </summary>
internal static class SdkNames
{
    public const string WorkflowType = "Temporalio.Workflows.Workflow";
    public const string ActivityOptionsType = "Temporalio.Workflows.ActivityOptions";
    public const string LocalActivityOptionsType = "Temporalio.Workflows.LocalActivityOptions";
    public const string ActivityExecutionContextType = "Temporalio.Activities.ActivityExecutionContext";
    public const string CompleteAsyncExceptionType = "Temporalio.Activities.CompleteAsyncException";
    public const string CancellationTokenType = "System.Threading.CancellationToken";
    public const string ExternalWorkflowHandleType = "Temporalio.Workflows.ExternalWorkflowHandle";
    public const string TemporalWorkerOptionsType = "Temporalio.Worker.TemporalWorkerOptions";
    public const string NexusWorkflowClientType = "Temporalio.Workflows.NexusWorkflowClient";

    /// <summary>
    /// Child-workflow entry points. <c>ExecuteChildWorkflowAsync</c> is the
    /// "start + await result" shortcut for <c>StartChildWorkflowAsync</c>.
    /// </summary>
    public static readonly ImmutableHashSet<string> ChildWorkflowStartMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "StartChildWorkflowAsync",
        "ExecuteChildWorkflowAsync");

    /// <summary>
    /// Nexus operation entry points invoked on a <c>NexusWorkflowClient</c>
    /// obtained from <c>Workflow.CreateNexusWorkflowClient</c>.
    /// </summary>
    public static readonly ImmutableHashSet<string> NexusOperationMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "StartNexusOperationAsync",
        "ExecuteNexusOperationAsync");

    /// <summary>
    /// Client entry points that start (and optionally await) a workflow.
    /// </summary>
    public static readonly ImmutableHashSet<string> ClientWorkflowStartMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "StartWorkflowAsync",
        "ExecuteWorkflowAsync");

    /// <summary>
    /// Workflow command methods that schedule commands or mutate workflow
    /// context. Calling these at module load or from a query handler is a bug.
    /// Versioning markers (Patched/DeprecatePatch) are intentionally excluded:
    /// they record a history marker but do not schedule or block work.
    /// </summary>
    public static readonly ImmutableHashSet<string> WorkflowCommandMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "DelayAsync",
        "WaitConditionAsync",
        "ExecuteActivityAsync",
        "ExecuteLocalActivityAsync",
        "ExecuteChildWorkflowAsync",
        "StartChildWorkflowAsync",
        "CreateContinueAsNewException",
        "UpsertTypedSearchAttributes",
        "UpsertMemo");

    /// <summary>
    /// The subset of workflow commands that block or yield the workflow task
    /// (activities, child workflows, timers, condition waits). Synchronous
    /// history-only commands (UpsertTypedSearchAttributes, UpsertMemo) and
    /// continue-as-new are excluded; a signal handler that upserts search
    /// attributes is synchronous and idiomatic.
    /// </summary>
    public static readonly ImmutableHashSet<string> WorkflowBlockingCommands = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "DelayAsync",
        "WaitConditionAsync",
        "ExecuteActivityAsync",
        "ExecuteLocalActivityAsync",
        "ExecuteChildWorkflowAsync",
        "StartChildWorkflowAsync");

    /// <summary>
    /// Temporal client and worker types that must never be referenced from
    /// workflow code (TMP3212).
    /// </summary>
    public static readonly ImmutableHashSet<string> ClientWorkerTypes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Temporalio.Client.ITemporalClient",
        "Temporalio.Client.TemporalClient",
        "Temporalio.Client.AsyncActivityHandle",
        "Temporalio.Client.ScheduleHandle",
        "Temporalio.Client.ScheduleClient",
        "Temporalio.Client.TemporalConnection",
        "Temporalio.Worker.TemporalWorker",
        "Temporalio.Worker.TemporalWorkerOptions",
        "Temporalio.Runtime.TemporalRuntime");

    public static bool IsWorkflowType(ITypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) == WorkflowType;

    public static bool IsWorkflowCommand(IMethodSymbol method) =>
        method.ContainingType is not null &&
        IsWorkflowType(method.ContainingType) &&
        WorkflowCommandMethods.Contains(method.Name);

    public static bool IsBlockingWorkflowCommand(IMethodSymbol method) =>
        method.ContainingType is not null &&
        IsWorkflowType(method.ContainingType) &&
        WorkflowBlockingCommands.Contains(method.Name);

    /// <summary>
    /// True if the method cancels an external workflow. Unlike most workflow
    /// commands, <c>ExternalWorkflowHandle.CancelAsync</c> issues its cancel
    /// request unconditionally and does not consult the workflow's cancellation
    /// token, so it remains safe in cancellation cleanup.
    /// </summary>
    public static bool IsExternalWorkflowCancel(IMethodSymbol method) =>
        method.Name == "CancelAsync" &&
        method.ContainingType is not null &&
        TypeNames.FullName(method.ContainingType) == ExternalWorkflowHandleType;
}
