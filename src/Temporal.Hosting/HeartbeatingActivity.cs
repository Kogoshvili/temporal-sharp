using Temporalio.Activities;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Base class for long-running activities that need to heartbeat progress, stay
/// alive, and resume from a checkpoint after a retry. Subclasses write their
/// <c>[Activity]</c> methods against this type's protected surface instead of
/// reaching into <see cref="ActivityExecutionContext.Current"/> directly.
/// </summary>
/// <remarks>
/// Two concerns are handled here, deliberately kept independent:
/// <list type="bullet">
/// <item>
/// <description><see cref="Heartbeat"/> records a progress checkpoint and
/// <see cref="LoadProgressAsync{T}"/> reads the last attempt's checkpoint back so
/// a retried activity can skip already-done work. The progress type is chosen per
/// call (generic method), so a single activity class can run several activities
/// with different progress shapes.</description>
/// </item>
/// <item>
/// <description><see cref="StartAutoHeartbeat"/> runs a background loop that keeps
/// the activity alive and lets cancellation reach it, without the caller threading
/// heartbeat calls through its own loop. The loop relays the last checkpoint rather
/// than sending an empty heartbeat, so a background tick never clobbers the progress
/// a retry depends on.</description>
/// </item>
/// </list>
/// The auto-heartbeat interval defaults to one third of the activity's
/// <c>HeartbeatTimeout</c> (clamped to a 1s minimum), falling back to 30s when no
/// heartbeat timeout is configured. The SDK throttles heartbeats internally, so the
/// background loop may tick more often than the server actually receives.
/// </remarks>
public abstract class HeartbeatingActivity
{
    private object?[]? _lastDetails;

    /// <summary>Gets the ambient activity execution context.</summary>
    protected ActivityExecutionContext Context => ActivityExecutionContext.Current;

    /// <summary>Gets the activity's cancellation token.</summary>
    protected CancellationToken CancellationToken => Context.CancellationToken;

    /// <summary>
    /// Records a heartbeat carrying the given details, and remembers them so the
    /// auto-heartbeat loop relays the latest checkpoint instead of an empty ping.
    /// </summary>
    /// <param name="details">Progress checkpoint to persist. Typically one immutable value.</param>
    protected void Heartbeat(params object?[] details)
    {
        _lastDetails = details;
        Context.Heartbeat(details);
    }

    /// <summary>
    /// Loads the progress checkpoint left by the previous attempt, if any, so the
    /// activity can resume rather than restart. Returns <see langword="default"/> when
    /// there are no prior heartbeat details (for example, the first attempt).
    /// </summary>
    /// <typeparam name="T">The progress type.</typeparam>
    /// <returns>The last checkpoint, or <see langword="default"/>.</returns>
    protected async Task<T?> LoadProgressAsync<T>()
    {
        if (Context.Info.HeartbeatDetails.Count == 0)
        {
            return default;
        }

        return await Context.Info.HeartbeatDetailAtAsync<T>(0).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts a background loop that heartbeats periodically, relaying the last
    /// recorded checkpoint (or an empty heartbeat when none has been recorded yet).
    /// Dispose the returned value to stop the loop; a <c>using</c> statement that spans
    /// the activity body is the intended pattern.
    /// </summary>
    /// <param name="interval">
    /// Tick interval. Defaults to one third of <c>HeartbeatTimeout</c> (clamped to a 1s
    /// minimum), or 30s when no heartbeat timeout is configured.
    /// </param>
    /// <returns>A handle that stops the loop when disposed.</returns>
    protected IDisposable StartAutoHeartbeat(TimeSpan? interval = null)
    {
        var context = Context;
        var cts = new CancellationTokenSource();
        var delay = interval ?? DeriveInterval(context);
        _ = RunLoopAsync(context, cts.Token, delay);
        return new HeartbeatDisposable(cts);
    }

    private async Task RunLoopAsync(
        ActivityExecutionContext context,
        CancellationToken cancellationToken,
        TimeSpan delay)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                context.Heartbeat(_lastDetails ?? Array.Empty<object?>());
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static TimeSpan DeriveInterval(ActivityExecutionContext context)
    {
        if (context.Info.HeartbeatTimeout is { } timeout)
        {
            var third = TimeSpan.FromTicks(timeout.Ticks / 3);
            return third < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : third;
        }

        return TimeSpan.FromSeconds(30);
    }

    private sealed class HeartbeatDisposable : IDisposable
    {
        private readonly CancellationTokenSource _cts;

        public HeartbeatDisposable(CancellationTokenSource cts) => _cts = cts;

        public void Dispose() => _cts.Cancel();
    }
}
