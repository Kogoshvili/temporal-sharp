// Stand-in Temporal types so the sample builds without the real SDK package.
// The analyzer matches these purely by name.
using System.Linq.Expressions;

namespace Temporalio.Workflows
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class WorkflowAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class WorkflowRunAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class WorkflowQueryAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class WorkflowSignalAttribute : System.Attribute { }

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
        public static SearchAttributeKey ForKeyword(string name) => new();
        public static SearchAttributeKey CreateKeyword(string name) => new();
        public static SearchAttributeKey CreateLong(string name) => new();
        public static SearchAttributeKey CreateDouble(string name) => new();
        public static SearchAttributeKey CreateDateTimeOffset(string name) => new();
        public SearchAttributeUpdate ValueSet(object? value) => new();
    }

    public sealed class SearchAttributeUpdate { }

    public sealed class WorkflowLogger
    {
        public void LogInformation(string message) { }
        public void LogWarning(string message) { }
    }

    public static class Workflow
    {
        public static System.DateTime UtcNow => default;
        public static System.Guid NewGuid() => default;
        public static System.Random Random => new();
        public static WorkflowLogger Logger => new();

        public static System.Threading.Tasks.Task DelayAsync(int millisecondsDelay)
            => System.Threading.Tasks.Task.CompletedTask;

        public static System.Collections.Generic.IDictionary<string, object> Signals { get; } =
            new System.Collections.Generic.Dictionary<string, object>();

        public static System.Collections.Generic.IDictionary<string, object> Updates { get; } =
            new System.Collections.Generic.Dictionary<string, object>();

        public static System.Threading.Tasks.Task ExecuteActivityAsync(
            string activity,
            System.Collections.Generic.IReadOnlyCollection<object?>? args,
            ActivityOptions options)
            => System.Threading.Tasks.Task.CompletedTask;

        public static System.Threading.Tasks.Task ExecuteActivityAsync(
            Expression<System.Func<object?>> activityCall,
            ActivityOptions options)
            => System.Threading.Tasks.Task.CompletedTask;

        public static System.Threading.Tasks.Task ExecuteLocalActivityAsync(
            string activity,
            System.Collections.Generic.IReadOnlyCollection<object?>? args,
            LocalActivityOptions options)
            => System.Threading.Tasks.Task.CompletedTask;

        public static System.Threading.Tasks.Task ExecuteLocalActivityAsync(
            Expression<System.Func<object?>> activityCall,
            LocalActivityOptions options)
            => System.Threading.Tasks.Task.CompletedTask;

        public static System.Threading.Tasks.Task ExecuteChildWorkflowAsync(
            string workflow,
            System.Collections.Generic.IReadOnlyCollection<object?>? args,
            ActivityOptions options)
            => System.Threading.Tasks.Task.CompletedTask;

        public static System.Threading.Tasks.Task ExecuteChildWorkflowAsync(
            Expression<System.Func<object?>> workflowRunCall,
            ActivityOptions options)
            => System.Threading.Tasks.Task.CompletedTask;

        public static ContinueAsNewException CreateContinueAsNewException(
            string workflow,
            System.Collections.Generic.IReadOnlyCollection<object?>? args,
            ContinueAsNewOptions options)
            => new ContinueAsNewException();

        public static ContinueAsNewException CreateContinueAsNewException(
            Expression<System.Func<object?>> workflowRunCall,
            ContinueAsNewOptions options)
            => new ContinueAsNewException();

        public static System.Threading.Tasks.Task WaitConditionAsync(
            System.Func<bool> conditionCheck,
            System.Threading.CancellationToken? cancellationToken = null)
            => System.Threading.Tasks.Task.CompletedTask;

        public static System.Threading.Tasks.Task<bool> WaitConditionAsync(
            System.Func<bool> conditionCheck,
            System.TimeSpan timeout,
            System.Threading.CancellationToken? cancellationToken = null)
            => System.Threading.Tasks.Task.FromResult(true);

        public static bool Patched(string patchId) => false;

        public static void DeprecatePatch(string patchId) { }

        public static void UpsertTypedSearchAttributes(params SearchAttributeUpdate[] updates) { }
    }
}

namespace Temporalio.Activities
{
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class ActivityAttribute : System.Attribute { }

    public sealed class ActivityExecutionContext
    {
        public static ActivityExecutionContext Current => new();
        public void Heartbeat(params object?[] details) { }
    }
}
