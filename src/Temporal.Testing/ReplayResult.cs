namespace Kogoshvili.Temporal.Testing;

/// <summary>
/// Outcome of a capture-and-replay run. <see cref="ReplayFailure"/> is null when
/// the workflow replayed deterministically; otherwise it holds the non-determinism
/// detected by <c>WorkflowReplayer</c>.
/// </summary>
public sealed class ReplayResult
{
    /// <summary>Initializes the result with the captured snapshot and the replay outcome.</summary>
    /// <param name="snapshotJson">The captured workflow event history as JSON.</param>
    /// <param name="replayFailure">The replay failure, or null when the replay was deterministic.</param>
    public ReplayResult(string snapshotJson, Exception? replayFailure)
    {
        SnapshotJson = snapshotJson;
        ReplayFailure = replayFailure;
    }

    /// <summary>The captured workflow event history as JSON (the snapshot).</summary>
    public string SnapshotJson { get; }

    /// <summary>The replay failure, or null when the replay was deterministic.</summary>
    public Exception? ReplayFailure { get; }

    /// <summary>True when the workflow replayed without divergence.</summary>
    public bool Succeeded => ReplayFailure is null;

    /// <summary>Throws <see cref="ReplayMismatchException"/> when the replay diverged.</summary>
    public void ThrowIfFailed()
    {
        if (ReplayFailure is not null)
        {
            throw new ReplayMismatchException(
                "Workflow replay diverged from the recorded history; the workflow is non-deterministic.",
                ReplayFailure);
        }
    }
}
