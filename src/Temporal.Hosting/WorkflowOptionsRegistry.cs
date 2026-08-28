using Microsoft.Extensions.Options;
using Temporalio.Client;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Builds a <see cref="WorkflowOptions"/> from <c>Temporal:Workflows</c>
/// configuration, layering the default preset, a per-workflow-type override, and
/// workflow-ID conventions, before finally applying any caller-supplied
/// overrides. Unlike <see cref="ActivityOptionsRegistry"/> (which is static
/// because sandboxed workflows cannot use DI), this is an injected service: the
/// workflow-start caller is a normal DI-enabled consumer.
/// </summary>
public sealed class WorkflowOptionsRegistry
{
    private readonly TemporalWorkflowOptions? workflows;

    /// <summary>Initializes a new instance of the <see cref="WorkflowOptionsRegistry"/> class.</summary>
    public WorkflowOptionsRegistry(IOptions<TemporalOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        workflows = options.Value.Workflows;
    }

    /// <summary>
    /// Builds a <see cref="WorkflowOptions"/> for starting the given workflow
    /// type. Precedence (lowest to highest): SDK defaults, the
    /// <c>Default</c> preset, the <c>ByType</c> override, then the explicit
    /// <paramref name="taskQueue"/>, <paramref name="workflowId"/>, and
    /// <paramref name="configure"/> arguments.
    /// </summary>
    /// <param name="workflowType">Workflow type name, used to look up the <c>ByType</c> override and ID convention.</param>
    /// <param name="taskQueue">Task queue; when null, resolved from <c>ByType</c> then <c>Default</c>.</param>
    /// <param name="workflowId">Explicit workflow ID; when null, the ID convention (or SDK default) applies.</param>
    /// <param name="configure">Final caller override, applied last and always wins.</param>
    /// <exception cref="InvalidOperationException">
    /// No task queue was resolvable (not passed, not in <c>ByType</c>, not in <c>Default</c>).
    /// </exception>
    public WorkflowOptions Build(
        string workflowType,
        string? taskQueue = null,
        string? workflowId = null,
        Action<WorkflowOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowType);

        var options = new WorkflowOptions();

        WorkflowOptionsFactory.Apply(workflows?.Default, options);

        if (workflows?.ByType is { } byType
            && byType.TryGetValue(workflowType, out var preset))
        {
            WorkflowOptionsFactory.Apply(preset, options);
        }

        options.TaskQueue = ResolveTaskQueue(taskQueue, options.TaskQueue, workflowType);
        options.Id = ResolveId(workflowId, workflowType, options.TaskQueue!);

        configure?.Invoke(options);

        return options;
    }

    private static string ResolveTaskQueue(string? explicitQueue, string? presetQueue, string workflowType)
    {
        var queue = explicitQueue ?? presetQueue;
        if (string.IsNullOrWhiteSpace(queue))
        {
            throw new InvalidOperationException(
                $"No task queue was resolved for workflow type '{workflowType}'. " +
                "Set 'Temporal:Workflows:Default:TaskQueue', a per-type 'TaskQueue' under " +
                "'Temporal:Workflows:ByType', or pass 'taskQueue' explicitly.");
        }

        return queue;
    }

    private string? ResolveId(string? explicitId, string workflowType, string taskQueue)
    {
        if (explicitId is not null)
        {
            return explicitId;
        }

        var format = ResolveFormat(workflows?.Id?.Format, WorkflowIdOptions.DefaultFormat);
        if (format is null)
        {
            return null;
        }

        return WorkflowIdFormatter.Format(format, workflowType, taskQueue);
    }

    private static string? ResolveFormat(string? configured, string fallback) =>
        configured switch
        {
            null => fallback,
            "" => null,
            _ => configured,
        };
}
