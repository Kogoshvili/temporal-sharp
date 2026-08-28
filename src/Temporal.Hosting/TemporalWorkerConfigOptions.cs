namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Per-queue worker configuration, bound from
/// <c>Temporal:Workers:&lt;queue&gt;</c>. Every tuning property is nullable:
/// <c>null</c> means "leave the SDK default untouched".
/// </summary>
public sealed class TemporalWorkerConfigOptions
{
    /// <summary>
    /// Gets or sets the namespace this worker polls, bound from
    /// <c>Temporal:Workers:&lt;queue&gt;:Namespace</c>. Falls back to the
    /// default namespace (<c>Temporal:Namespace</c>) when unset. An explicit
    /// namespace passed to <c>AddTemporalWorker</c> wins over this value.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Gets or sets worker deployment/versioning configuration. When set (and
    /// <see cref="TemporalWorkerDeploymentOptions.UseWorkerVersioning"/> is
    /// enabled), the worker opts into Worker Versioning and reports this
    /// deployment version to the server on every poll.
    /// </summary>
    public TemporalWorkerDeploymentOptions? Deployment { get; set; }

    /// <summary>Maximum number of activities processed concurrently.</summary>
    public int? MaxConcurrentActivities { get; set; }

    /// <summary>Maximum number of workflow tasks processed concurrently.</summary>
    public int? MaxConcurrentWorkflowTasks { get; set; }

    /// <summary>Maximum number of local activities processed concurrently.</summary>
    public int? MaxConcurrentLocalActivities { get; set; }

    /// <summary>Maximum number of concurrent activity task poll requests.</summary>
    public int? MaxConcurrentActivityTaskPolls { get; set; }

    /// <summary>Maximum number of concurrent workflow task poll requests.</summary>
    public int? MaxConcurrentWorkflowTaskPolls { get; set; }

    /// <summary>Grace period the worker allows in-flight tasks to finish on shutdown.</summary>
    public TimeSpan? GracefulShutdownTimeout { get; set; }

    /// <summary>Maximum number of cached workflow instances.</summary>
    public int? MaxCachedWorkflows { get; set; }
}
