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
    public sealed class WorkflowQueryAttribute : System.Attribute { public string? Name { get; set; } }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class WorkflowSignalAttribute : System.Attribute { public string? Name { get; set; } }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class WorkflowUpdateAttribute : System.Attribute { public string? Name { get; set; } }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class WorkflowUpdateValidatorAttribute : System.Attribute { public string? Name { get; set; } }

    [System.AttributeUsage(System.AttributeTargets.Constructor)]
    public sealed class WorkflowInitAttribute : System.Attribute { }

    public sealed class RetryPolicy { }

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

    public sealed class ContinueAsNewOptions { }

    public sealed class ChildWorkflowOptions { }

    public sealed class ChildWorkflowHandle
    {
        public string Id => string.Empty;

        public System.Threading.Tasks.Task GetResultAsync() => System.Threading.Tasks.Task.CompletedTask;
    }

    public sealed class NexusOperationOptions { }

    public sealed class NexusWorkflowClient
    {
        public NexusWorkflowOperationHandle StartNexusOperationAsync(
            string operation,
            object? arg,
            NexusOperationOptions? options)
            => new NexusWorkflowOperationHandle();

        public NexusWorkflowOperationHandle StartNexusOperationAsync<TService, TResult>(
            Expression<System.Func<TService, System.Threading.Tasks.Task<TResult>>> operationCall,
            NexusOperationOptions? options)
            => new NexusWorkflowOperationHandle();
    }

    public sealed class NexusWorkflowOperationHandle
    {
        public string OperationToken => string.Empty;

        public System.Threading.Tasks.Task GetResultAsync() => System.Threading.Tasks.Task.CompletedTask;
    }

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

    public sealed class DeterministicRandom : System.Random
    {
    }

    public static class Workflow
    {
        public static System.DateTime UtcNow => default;
        public static System.Guid NewGuid() => default;
        public static DeterministicRandom Random => new();
        public static WorkflowLogger Logger => new();

        public static class Unsafe
        {
            public static bool IsReplaying => false;

            public static bool IsReplayingHistoryEvents => false;
        }

        public static System.Threading.CancellationToken CancellationToken => default;
        public static bool ContinueAsNewSuggested => false;
        public static bool AllHandlersFinished => false;
        public static bool TargetWorkerDeploymentVersionChanged => false;

        public static System.Threading.Tasks.Task NonCancellableAsync(System.Func<System.Threading.Tasks.Task> work)
            => System.Threading.Tasks.Task.CompletedTask;

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

        public static ChildWorkflowHandle StartChildWorkflowAsync(
            string workflow,
            System.Collections.Generic.IReadOnlyCollection<object?>? args,
            ChildWorkflowOptions? options)
            => new ChildWorkflowHandle();

        public static ChildWorkflowHandle StartChildWorkflowAsync<TWorkflow, TResult>(
            Expression<System.Func<TWorkflow, System.Threading.Tasks.Task<TResult>>> workflowRunCall,
            ChildWorkflowOptions? options)
            => new ChildWorkflowHandle();

        public static NexusWorkflowClient CreateNexusWorkflowClient(
            string service,
            NexusOperationOptions? options = null)
            => new NexusWorkflowClient();

        public static NexusWorkflowClient CreateNexusWorkflowClient<TService>(NexusOperationOptions? options = null)
            => new NexusWorkflowClient();

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
        public System.Threading.CancellationToken CancellationToken => default;
        public Temporalio.Workflows.WorkflowLogger Log => new();
    }
}

namespace Temporalio.Client
{
    public sealed class WorkflowOptions
    {
        public string? Id { get; set; }
    }

    public sealed class StartWorkflowOptions
    {
        public string? Id { get; set; }

        public string? TaskQueue { get; set; }
    }

    public sealed class WorkflowHandle<TResult>
    {
        public System.Threading.Tasks.Task<TResult> GetResultAsync()
            => System.Threading.Tasks.Task.FromResult<TResult>(default!);
    }

    public interface ITemporalClient { }

    public sealed class TemporalClient : ITemporalClient
    {
        public System.Threading.Tasks.Task<WorkflowHandle<TResult>> StartWorkflowAsync<TWorkflow, TResult>(
            System.Linq.Expressions.Expression<System.Func<TWorkflow, System.Threading.Tasks.Task<TResult>>> workflowRunCall,
            StartWorkflowOptions options)
            => System.Threading.Tasks.Task.FromResult(new WorkflowHandle<TResult>());

        public System.Threading.Tasks.Task<WorkflowHandle<TResult>> ExecuteWorkflowAsync<TWorkflow, TResult>(
            System.Linq.Expressions.Expression<System.Func<TWorkflow, System.Threading.Tasks.Task<TResult>>> workflowRunCall,
            StartWorkflowOptions options)
            => System.Threading.Tasks.Task.FromResult(new WorkflowHandle<TResult>());

        public System.Threading.Tasks.Task ExecuteActivityAsync(
            string activity,
            System.Collections.Generic.IReadOnlyCollection<object?>? args,
            Temporalio.Workflows.ActivityOptions options)
            => System.Threading.Tasks.Task.CompletedTask;
    }

    public sealed class WorkflowClient
    {
        public System.Threading.Tasks.Task<string> StartWorkflowAsync(
            System.Linq.Expressions.Expression<System.Func<object?>> workflowRunCall,
            WorkflowOptions options)
            => System.Threading.Tasks.Task.FromResult("");
    }
}

namespace Temporalio.Worker
{
    public sealed class TemporalWorkerOptions
    {
        public TemporalWorkerOptions(string taskQueue)
        {
            TaskQueue = taskQueue;
        }

        public string? TaskQueue { get; set; }

        public TemporalWorkerOptions AddWorkflow<TWorkflow>() => this;

        public TemporalWorkerOptions AddActivity(System.Func<System.Threading.Tasks.Task> activity) => this;

        public TemporalWorkerOptions AddActivity(System.Action activity) => this;
    }

    public sealed class TemporalWorker
    {
        public TemporalWorker(Temporalio.Client.ITemporalClient client, TemporalWorkerOptions options) { }
    }
}

namespace Temporalio.Exceptions
{
    public sealed class ApplicationFailureException : System.Exception
    {
        public ApplicationFailureException(string message, string? errorType = null, bool nonRetryable = false)
        {
        }
    }
}
