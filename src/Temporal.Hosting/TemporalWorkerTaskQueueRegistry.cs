namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Tracks the task queues for which hosted workers are registered. The health
/// check uses this to verify each queue has an active poller. Populated by
/// <c>AddTemporalWorker</c>.
/// </summary>
public sealed class TemporalWorkerTaskQueueRegistry
{
    private readonly object sync = new();
    private readonly List<string> taskQueues = new();

    /// <summary>Gets the registered task queue names.</summary>
    public IReadOnlyCollection<string> TaskQueues
    {
        get
        {
            lock (sync)
            {
                return taskQueues.ToArray();
            }
        }
    }

    internal void Register(string taskQueue)
    {
        lock (sync)
        {
            if (!taskQueues.Contains(taskQueue))
            {
                taskQueues.Add(taskQueue);
            }
        }
    }
}
