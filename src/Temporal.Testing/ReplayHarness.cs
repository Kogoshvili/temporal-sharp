using System.Linq.Expressions;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Testing;
using Temporalio.Worker;

namespace Kogoshvili.Temporal.Testing;

/// <summary>
/// A replay/regression test harness: starts a Temporal test environment, runs a
/// workflow to completion, snapshots its event history, and replays that history
/// through <c>WorkflowReplayer</c> to surface non-determinism.
/// </summary>
/// <remarks>
/// Intended for use as an xUnit fixture (implement <c>IAsyncLifetime</c> and
/// dispose the harness in teardown) or via <c>await using</c>.
/// </remarks>
public sealed class ReplayHarness : IAsyncDisposable
{
    private ReplayHarness(WorkflowEnvironment environment)
    {
        Environment = environment;
    }

    public WorkflowEnvironment Environment { get; }

    public ITemporalClient Client => Environment.Client;

    /// <summary>Starts a time-skipping test environment (recommended for fast runs).</summary>
    public static Task<ReplayHarness> StartTimeSkippingAsync() =>
        StartTimeSkippingAsync(new WorkflowEnvironmentStartTimeSkippingOptions());

    /// <summary>Starts a time-skipping test environment with custom options.</summary>
    public static async Task<ReplayHarness> StartTimeSkippingAsync(WorkflowEnvironmentStartTimeSkippingOptions options)
    {
        var environment = await WorkflowEnvironment.StartTimeSkippingAsync(options);
        return new ReplayHarness(environment);
    }

    /// <summary>Starts a full local Temporal server (no time skipping).</summary>
    public static Task<ReplayHarness> StartLocalAsync() =>
        StartLocalAsync(new WorkflowEnvironmentStartLocalOptions());

    /// <summary>Starts a full local Temporal server with custom options.</summary>
    public static async Task<ReplayHarness> StartLocalAsync(WorkflowEnvironmentStartLocalOptions options)
    {
        var environment = await WorkflowEnvironment.StartLocalAsync(options);
        return new ReplayHarness(environment);
    }

    /// <summary>
    /// Runs the workflow to completion with the supplied worker and captures its
    /// event history.
    /// </summary>
    public async Task<(TResult Result, WorkflowHistory History)> CaptureAsync<TWorkflow, TResult>(
        TemporalWorkerOptions workerOptions,
        Expression<Func<TWorkflow, Task<TResult>>> runCall,
        WorkflowOptions startOptions)
    {
        var handle = await Client.StartWorkflowAsync(runCall, startOptions);
        using var worker = new TemporalWorker(Client, workerOptions);

        TResult result = default!;
        await worker.ExecuteAsync(async () =>
        {
            result = await handle.GetResultAsync();
        });

        return (result, await handle.FetchHistoryAsync());
    }

    /// <summary>
    /// Replays a captured history through <c>WorkflowReplayer</c> without
    /// throwing, returning the raw replay result.
    /// </summary>
    public async Task<WorkflowReplayResult> ReplayAsync<TWorkflow>(WorkflowHistory history)
    {
        var replayer = new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<TWorkflow>());
        return await replayer.ReplayWorkflowAsync(history, throwOnReplayFailure: false);
    }

    /// <summary>
    /// Captures and replays a workflow in one step, returning a
    /// <see cref="ReplayResult"/> whose <see cref="ReplayResult.Succeeded"/>
    /// reflects whether the replay was deterministic.
    /// </summary>
    public async Task<ReplayResult> VerifyAsync<TWorkflow, TResult>(
        TemporalWorkerOptions workerOptions,
        Expression<Func<TWorkflow, Task<TResult>>> runCall,
        WorkflowOptions startOptions)
    {
        var (_, history) = await CaptureAsync<TWorkflow, TResult>(workerOptions, runCall, startOptions);
        var replay = await ReplayAsync<TWorkflow>(history);
        return new ReplayResult(Snapshot.ToJson(history), replay.ReplayFailure);
    }

    public async ValueTask DisposeAsync() => await Environment.DisposeAsync();
}
