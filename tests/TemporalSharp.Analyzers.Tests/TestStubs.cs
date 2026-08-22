namespace TemporalSharp.Analyzers.Tests;

/// <summary>
/// Stub Temporal types used by tests so the analyzers (which match by name) can
/// be exercised without referencing the real Temporal SDK.
/// </summary>
internal static class TestStubs
{
    public const string Attributes = """
        namespace Temporalio.Workflows
        {
            public sealed class WorkflowAttribute : System.Attribute { }
            public sealed class WorkflowRunAttribute : System.Attribute { }
        }

        namespace Temporalio.Activities
        {
            public sealed class ActivityAttribute : System.Attribute { }
        }
        """;

    public const string Sdk = """
        namespace Temporalio.Workflows
        {
            public sealed class ActivityOptions
            {
                public System.TimeSpan? StartToCloseTimeout { get; set; }
                public System.TimeSpan? ScheduleToCloseTimeout { get; set; }
                public System.TimeSpan? HeartbeatTimeout { get; set; }
                public string? TaskQueue { get; set; }
            }

            public sealed class LocalActivityOptions
            {
                public System.TimeSpan? StartToCloseTimeout { get; set; }
                public System.TimeSpan? ScheduleToCloseTimeout { get; set; }
                public System.TimeSpan? HeartbeatTimeout { get; set; }
                public string? TaskQueue { get; set; }
            }

            public sealed class ContinueAsNewOptions { }

            public sealed class ContinueAsNewException : System.Exception
            {
                public ContinueAsNewException() { }
            }

            public sealed class SearchAttributeKey
            {
                public static SearchAttributeKey ForKeyword(string name) => new SearchAttributeKey();
            }

            public static class Workflow
            {
                public static bool Patched(string patchId) => false;

                public static void DeprecatePatch(string patchId) { }

                public static void UpsertTypedSearchAttributes(params SearchAttributeKey[] updates) { }
                public static System.Threading.Tasks.Task ExecuteActivityAsync(
                    string activity,
                    System.Collections.Generic.IReadOnlyCollection<object?>? args,
                    ActivityOptions options)
                    => System.Threading.Tasks.Task.CompletedTask;

                public static System.Threading.Tasks.Task ExecuteActivityAsync(
                    System.Linq.Expressions.Expression<System.Func<object?>> activityCall,
                    ActivityOptions options)
                    => System.Threading.Tasks.Task.CompletedTask;

                public static System.Threading.Tasks.Task ExecuteChildWorkflowAsync(
                    string workflow,
                    System.Collections.Generic.IReadOnlyCollection<object?>? args,
                    ActivityOptions options)
                    => System.Threading.Tasks.Task.CompletedTask;

                public static ContinueAsNewException CreateContinueAsNewException(
                    string workflow,
                    System.Collections.Generic.IReadOnlyCollection<object?>? args,
                    ContinueAsNewOptions options)
                    => new ContinueAsNewException();

                public static ContinueAsNewException CreateContinueAsNewException(
                    System.Linq.Expressions.Expression<System.Func<object?>> workflowRunCall,
                    ContinueAsNewOptions options)
                    => new ContinueAsNewException();
            }
        }

        namespace Temporalio.Activities
        {
            public sealed class ActivityExecutionContext
            {
                public static ActivityExecutionContext Current => new ActivityExecutionContext();
                public void Heartbeat(params object?[] details) { }
            }
        }
        """;
}
