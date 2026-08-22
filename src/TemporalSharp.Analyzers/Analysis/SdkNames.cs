namespace TemporalSharp.Analyzers.Analysis;

/// <summary>
/// Fully-qualified names of Temporal .NET SDK types and members used for
/// matching, without referencing the SDK assembly.
/// </summary>
internal static class SdkNames
{
    public const string WorkflowType = "Temporalio.Workflows.Workflow";
    public const string ActivityOptionsType = "Temporalio.Workflows.ActivityOptions";
    public const string LocalActivityOptionsType = "Temporalio.Workflows.LocalActivityOptions";
}
