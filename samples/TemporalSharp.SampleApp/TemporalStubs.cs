// Stand-in Temporal types so the sample builds without the real SDK package.
// The analyzer matches these purely by name.
namespace Temporalio.Workflows
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class WorkflowAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class WorkflowRunAttribute : System.Attribute { }

    public sealed class ActivityOptions
    {
        public System.TimeSpan? StartToCloseTimeout { get; set; }
        public System.TimeSpan? ScheduleToCloseTimeout { get; set; }
        public string? TaskQueue { get; set; }
    }

    public static class Workflow
    {
        public static System.DateTime UtcNow => default;
        public static System.Guid NewGuid() => default;
        public static System.Threading.Tasks.Task DelayAsync(int millisecondsDelay)
            => System.Threading.Tasks.Task.CompletedTask;

        public static System.Threading.Tasks.Task ExecuteActivityAsync(
            string activity,
            System.Collections.Generic.IReadOnlyCollection<object?>? args,
            ActivityOptions options)
            => System.Threading.Tasks.Task.CompletedTask;

        public static System.Threading.Tasks.Task ExecuteActivityAsync(
            System.Linq.Expressions.Expression<System.Func<object?>> activityCall,
            ActivityOptions options)
            => System.Threading.Tasks.Task.CompletedTask;
    }
}

namespace Temporalio.Activities
{
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class ActivityAttribute : System.Attribute { }
}
