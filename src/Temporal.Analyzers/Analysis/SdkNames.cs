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
    public const string WorkflowOptionsType = "Temporalio.Client.WorkflowOptions";
    public const string ClientNamespace = "Temporalio.Client";

    /// <summary>
    /// Workflow command methods that schedule commands or mutate workflow
    /// context. Calling these at module load or from a query handler is a bug.
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
        "Patched",
        "DeprecatePatch",
        "SignalExternalWorkflowAsync",
        "ExecuteUpdateAsync");

    /// <summary>
    /// Temporal client and worker types that must never be referenced from
    /// workflow code (TMP3212).
    /// </summary>
    public static readonly ImmutableHashSet<string> ClientWorkerTypes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Temporalio.Client.ITemporalClient",
        "Temporalio.Client.TemporalClient",
        "Temporalio.Client.AsyncActivityHandle",
        "Temporalio.Worker.TemporalWorker",
        "Temporalio.Worker.TemporalWorkerOptions",
        "Temporalio.Runtime.TemporalRuntime");

    public static bool IsWorkflowType(ITypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) == WorkflowType;

    public static bool IsWorkflowCommand(IMethodSymbol method) =>
        method.ContainingType is not null &&
        IsWorkflowType(method.ContainingType) &&
        WorkflowCommandMethods.Contains(method.Name);
}
