namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Per-queue worker tuning, bound from <c>Temporal:Workers:&lt;queue&gt;</c>. Every
/// property is nullable: <c>null</c> means "leave the SDK default untouched".
/// </summary>
public sealed class TemporalWorkerTuningOptions
{
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
