using System.Text.RegularExpressions;
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
    private static readonly Regex GuidPlaceholder = new(
        @"\{Guid(?::(?<format>[NDBPX]))?\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
    /// <paramref name="workflowId"/> and <paramref name="configure"/> arguments.
    /// </summary>
    /// <param name="workflowType">Workflow type name, used to look up the <c>ByType</c> override and ID convention.</param>
    /// <param name="taskQueue">Task queue the workflow runs on (required by the SDK).</param>
    /// <param name="workflowId">Explicit workflow ID; when null, the ID convention (or SDK default) applies.</param>
    /// <param name="configure">Final caller override, applied last and always wins.</param>
    public WorkflowOptions Build(
        string workflowType,
        string taskQueue,
        string? workflowId = null,
        Action<WorkflowOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowType);
        ArgumentException.ThrowIfNullOrEmpty(taskQueue);

        var options = new WorkflowOptions
        {
            Id = ResolveId(workflowId, workflowType, taskQueue),
            TaskQueue = taskQueue,
        };

        WorkflowOptionsFactory.Apply(workflows?.Default, options);

        if (workflows?.ByType is { } byType
            && byType.TryGetValue(workflowType, out var preset))
        {
            WorkflowOptionsFactory.Apply(preset, options);
        }

        configure?.Invoke(options);

        return options;
    }

    private string? ResolveId(string? explicitId, string workflowType, string taskQueue)
    {
        if (explicitId is not null)
        {
            return explicitId;
        }

        var format = workflows?.Id?.Format;
        if (string.IsNullOrWhiteSpace(format))
        {
            return null;
        }

        var id = GuidPlaceholder.Replace(
            format,
            match => Guid.NewGuid().ToString(match.Groups["format"].Success ? match.Groups["format"].Value : "N"));

        return id
            .Replace("{Type}", workflowType, StringComparison.Ordinal)
            .Replace("{Queue}", taskQueue, StringComparison.Ordinal);
    }
}
