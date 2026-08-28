using Temporalio.Exceptions;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Options controlling how a <see cref="Saga"/> runs its compensation operations.
/// Mirrors the Java SDK's <c>Saga.Options</c>.
/// </summary>
public sealed class SagaOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether compensations run in parallel.
    /// When <see langword="false"/> (the default), compensations run in reverse
    /// registration order (LIFO).
    /// </summary>
    public bool ParallelCompensation { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to continue running the remaining
    /// compensations after one fails. Only applies when
    /// <see cref="ParallelCompensation"/> is <see langword="false"/>; the default
    /// (<see langword="false"/>) stops at the first failure and rethrows.
    /// </summary>
    public bool ContinueWithError { get; init; }
}

/// <summary>
/// Collects compensation operations for a saga and runs them when a step fails.
/// Compensations are registered <em>before</em> the forward activity they undo,
/// then unwound via <see cref="CompensateAsync"/> from a catch block.
/// </summary>
/// <remarks>
/// This is a port of the Java SDK's <c>Saga</c> helper. Compensations are plain
/// <c>Func&lt;Task&gt;</c> closures that typically call
/// <c>Workflow.ExecuteActivityAsync(...)</c>, so ordinary activity options
/// (retry policy, timeouts) apply unchanged. Sequential compensation runs in
/// LIFO order and rethrows the first failure by default; set
/// <see cref="SagaOptions.ContinueWithError"/> to swallow and continue. Parallel
/// compensation runs every operation and surfaces all failures as an
/// <see cref="AggregateException"/>.
/// </remarks>
public sealed class Saga
{
    private readonly SagaOptions _options;
    private readonly List<Func<Task>> _compensations = new();

    /// <summary>Initializes a new instance of the <see cref="Saga"/> class.</summary>
    /// <param name="options">Compensation behavior; defaults are used when omitted.</param>
    public Saga(SagaOptions? options = null)
    {
        _options = options ?? new SagaOptions();
    }

    /// <summary>Registers an asynchronous compensation operation.</summary>
    /// <param name="compensation">The compensation to run during rollback.</param>
    public void AddCompensation(Func<Task> compensation)
    {
        ArgumentNullException.ThrowIfNull(compensation);
        _compensations.Add(compensation);
    }

    /// <summary>Registers a synchronous compensation operation.</summary>
    /// <param name="compensation">The compensation to run during rollback.</param>
    public void AddCompensation(Action compensation)
    {
        ArgumentNullException.ThrowIfNull(compensation);
        _compensations.Add(() =>
        {
            compensation();
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Runs the registered compensations. In sequential mode they run in reverse
    /// (LIFO) order; in parallel mode they all run and failures are aggregated.
    /// </summary>
    public async Task CompensateAsync()
    {
        if (_options.ParallelCompensation)
        {
            await CompensateInParallelAsync();
            return;
        }

        for (var i = _compensations.Count - 1; i >= 0; i--)
        {
            try
            {
                await _compensations[i]();
            }
            catch (Exception)
            {
                if (!_options.ContinueWithError)
                {
                    throw;
                }
            }
        }
    }

    private async Task CompensateInParallelAsync()
    {
        var tasks = _compensations.Select(c => c()).ToList();

        var errors = new List<Exception>();
        foreach (var task in tasks)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }

        if (errors.Count > 0)
        {
            throw new ApplicationFailureException(
                "One or more saga compensations failed",
                new AggregateException(errors));
        }
    }
}
