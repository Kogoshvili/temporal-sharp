using System.Runtime.CompilerServices;
using Temporalio.Client;
using Temporalio.Worker;

namespace Kogoshvili.Temporal.Testing;

/// <summary>
/// Replays workflow event histories from a fixed source — checked-in JSON
/// golden files or a live Temporal service — without spinning up a local test
/// environment. Use <see cref="ReplayHarness"/> when you want to capture and
/// replay a workflow locally in one step.
/// </summary>
public static class Replay
{
    private static WorkflowReplayer CreateReplayer<TWorkflow>() =>
        new(new WorkflowReplayerOptions().AddWorkflow<TWorkflow>());

    /// <summary>
    /// Replays a single event history serialized as JSON — e.g. a golden file
    /// exported from the Temporal CLI (<c>temporal workflow show --output json</c>)
    /// or the web UI, checked into the repo.
    /// </summary>
    public static Task<WorkflowReplayResult> FromJsonAsync<TWorkflow>(
        string historyJson, string workflowId) =>
        CreateReplayer<TWorkflow>().ReplayWorkflowAsync(
            Snapshot.FromJson(historyJson, workflowId), throwOnReplayFailure: false);

    /// <summary>
    /// Replays every history file in a directory matching <paramref name="pattern"/>.
    /// File names (without extension) are used as workflow ids.
    /// </summary>
    public static async Task<IReadOnlyList<WorkflowReplayResult>> FromDirectoryAsync<TWorkflow>(
        string directory, string pattern = "*.json")
    {
        var replayer = CreateReplayer<TWorkflow>();
        var results = new List<WorkflowReplayResult>();
        foreach (var file in Directory.EnumerateFiles(directory, pattern))
        {
            var workflowId = Path.GetFileNameWithoutExtension(file);
            var json = await File.ReadAllTextAsync(file);
            results.Add(await replayer.ReplayWorkflowAsync(
                Snapshot.FromJson(json, workflowId), throwOnReplayFailure: false));
        }

        return results;
    }

    /// <summary>
    /// Replays the recorded histories for a workflow type from a live Temporal
    /// service (Cloud or self-hosted). Supply an already-configured and
    /// authenticated <c>ITemporalClient</c> (e.g. from
    /// <c>TemporalConfig.ConnectAsync()</c>) — Cloud mTLS/API-key setup is the
    /// caller's responsibility.
    /// </summary>
    /// <param name="client">Authenticated Temporal client.</param>
    /// <param name="workflowType">Workflow type to replay, e.g. <c>SayHello</c>.</param>
    /// <param name="executionStatus">Execution status to filter on, or <c>null</c> for all. Defaults to <c>Completed</c>.</param>
    /// <param name="limit">Maximum number of histories to replay, or <c>null</c> for all. Applied as a total-count cap.</param>
    public static IAsyncEnumerable<WorkflowReplayResult> FromServerAsync<TWorkflow>(
        ITemporalClient client,
        string workflowType,
        string? executionStatus = "Completed",
        int? limit = null)
    {
        var replayer = CreateReplayer<TWorkflow>();
        var query = BuildQuery(workflowType, executionStatus);
        return TakeAsync(replayer.ReplayWorkflowsAsync(client.ListWorkflowHistoriesAsync(query)), limit);
    }

    private static string BuildQuery(string workflowType, string? executionStatus)
    {
        var query = $"WorkflowType = '{workflowType}'";
        if (!string.IsNullOrWhiteSpace(executionStatus))
        {
            query += $" AND ExecutionStatus = '{executionStatus}'";
        }

        return query;
    }

    private static async IAsyncEnumerable<WorkflowReplayResult> TakeAsync(
        IAsyncEnumerable<WorkflowReplayResult> source,
        int? limit,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var count = 0;
        await foreach (var result in source.WithCancellation(cancellationToken))
        {
            if (limit is not null && count >= limit)
            {
                yield break;
            }

            yield return result;
            count++;
        }
    }
}
