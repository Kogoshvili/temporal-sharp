namespace Kogoshvili.Temporal.Analyzers.Tests;

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
            public sealed class WorkflowQueryAttribute : System.Attribute { public string? Name { get; set; } }
            public sealed class WorkflowSignalAttribute : System.Attribute { public string? Name { get; set; } }
            public sealed class WorkflowUpdateAttribute : System.Attribute { public string? Name { get; set; } }
            public sealed class WorkflowUpdateValidatorAttribute : System.Attribute { public string? Name { get; set; } }
            public sealed class WorkflowInitAttribute : System.Attribute { }
        }

        namespace Temporalio.Activities
        {
            public sealed class ActivityAttribute : System.Attribute { }
        }

        namespace Temporalio.Client
        {
            public sealed class WorkflowOptions { public string? Id { get; set; } }
            public interface ITemporalClient { }
            public sealed class TemporalClient : ITemporalClient { }
            public sealed class WorkflowClient
            {
                public System.Threading.Tasks.Task<string> StartWorkflowAsync(
                    System.Linq.Expressions.Expression<System.Func<object?>> workflowRunCall,
                    WorkflowOptions options)
                    => System.Threading.Tasks.Task.FromResult("");
            }
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
                public RetryPolicy? RetryPolicy { get; set; }
                public string? TaskQueue { get; set; }
            }

            public sealed class LocalActivityOptions
            {
                public System.TimeSpan? StartToCloseTimeout { get; set; }
                public System.TimeSpan? ScheduleToCloseTimeout { get; set; }
                public System.TimeSpan? HeartbeatTimeout { get; set; }
                public RetryPolicy? RetryPolicy { get; set; }
                public string? TaskQueue { get; set; }
            }

            public sealed class RetryPolicy { }

            public sealed class ContinueAsNewOptions { }

            public sealed class ContinueAsNewException : System.Exception
            {
                public ContinueAsNewException() { }
            }

            public sealed class SearchAttributeKey
            {
                public static SearchAttributeKey ForKeyword(string name) => new SearchAttributeKey();
                public SearchAttributeUpdate ValueSet(object? value) => new SearchAttributeUpdate();
                public SearchAttributeUpdate ValueUnset() => new SearchAttributeUpdate();
                public SearchAttributeUpdate ValueNull() => new SearchAttributeUpdate();
            }

            public sealed class SearchAttributeUpdate { }

            public static class Workflow
            {
                public static System.DateTime UtcNow => default;

                public static System.Guid NewGuid() => default;

                public static System.Random Random => new System.Random(0);

                public static bool Patched(string patchId) => false;

                public static void DeprecatePatch(string patchId) { }

                public static bool IsCancellationRequested => false;

                public static bool ContinueAsNewSuggested => false;

                public static System.Threading.Tasks.Task AllHandlersFinished => System.Threading.Tasks.Task.CompletedTask;

                public static System.Threading.Tasks.Task NonCancellableAsync(System.Func<System.Threading.Tasks.Task> work)
                    => System.Threading.Tasks.Task.CompletedTask;

                public static System.Threading.Tasks.Task DelayAsync(int millisecondsDelay)
                    => System.Threading.Tasks.Task.CompletedTask;

                public static void UpsertTypedSearchAttributes(params SearchAttributeUpdate[] updates) { }

                public static System.Collections.Generic.IDictionary<string, object> Signals { get; } = new System.Collections.Generic.Dictionary<string, object>();
                public static System.Threading.Tasks.Task ExecuteActivityAsync(
                    string activity,
                    System.Collections.Generic.IReadOnlyCollection<object?>? args,
                    ActivityOptions options)
                    => System.Threading.Tasks.Task.CompletedTask;

                public static System.Threading.Tasks.Task ExecuteActivityAsync(
                    System.Linq.Expressions.Expression<System.Func<object?>> activityCall,
                    ActivityOptions options)
                    => System.Threading.Tasks.Task.CompletedTask;

                public static System.Threading.Tasks.Task ExecuteLocalActivityAsync(
                    string activity,
                    System.Collections.Generic.IReadOnlyCollection<object?>? args,
                    LocalActivityOptions options)
                    => System.Threading.Tasks.Task.CompletedTask;

                public static System.Threading.Tasks.Task ExecuteLocalActivityAsync(
                    System.Linq.Expressions.Expression<System.Func<object?>> activityCall,
                    LocalActivityOptions options)
                    => System.Threading.Tasks.Task.CompletedTask;

                public static System.Threading.Tasks.Task WaitConditionAsync(
                    System.Func<bool> conditionCheck,
                    System.Threading.CancellationToken? cancellationToken = null)
                    => System.Threading.Tasks.Task.CompletedTask;

                public static System.Threading.Tasks.Task<bool> WaitConditionAsync(
                    System.Func<bool> conditionCheck,
                    System.TimeSpan timeout,
                    System.Threading.CancellationToken? cancellationToken = null)
                    => System.Threading.Tasks.Task.FromResult(true);

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
                public System.Threading.CancellationToken CancellationToken => default;
                public object Log => new object();
            }
        }
        """;
}
