using Temporalio.Workflows;

namespace Kogoshvili.Temporal.SampleApp;

// Determinism violations (TMP01xx). Non-deterministic APIs on the workflow
// replay path. See the "Replacement" column in RULES.md for the fix.
[Workflow]
public class DeterminismViolations
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        // TMP0101 — wall-clock time
        _ = DateTime.Now;
        _ = DateTimeOffset.UtcNow;
        _ = Environment.TickCount64;

        // TMP0102 — Stopwatch elapsed wall-clock time
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _ = stopwatch.Elapsed;

        // TMP0111 — sleep / block
        System.Threading.Thread.Sleep(10);
        _ = System.Threading.Tasks.Task.Delay(100);
        System.Threading.Tasks.Task.Delay(100).Wait();

        // TMP0121 — randomness / identity
        _ = Guid.NewGuid();
        _ = System.Random.Shared.Next();

        // TMP0131 — I/O and environment access
        _ = Environment.GetEnvironmentVariable("HOME");

        // TMP0113 — ConfigureAwait(false) leaves the workflow context
        _ = System.Threading.Tasks.Task.CompletedTask.ConfigureAwait(false);

        // TMP0141 — concurrency (no Workflow.* replacement)
        _ = System.Threading.ThreadPool.QueueUserWorkItem(_ => { });

        // TMP0146 — task scheduling on the default scheduler
        _ = System.Threading.Tasks.Task.Run(() => { });
        _ = System.Threading.Tasks.Task.Factory.StartNew(() => { });

        // TMP0142 — blocking synchronization primitive
        lock (this) { }

        // TMP0147 — blocking primitive with a deterministic replacement
        var semaphore = new System.Threading.SemaphoreSlim(1);
        semaphore.Wait();

        // TMP0143 — raw task scheduling
        var task = System.Threading.Tasks.Task.CompletedTask;
        _ = System.Threading.Tasks.Task.WhenAll(task);
        _ = System.Threading.Tasks.Task.WhenAny(task);

        // TMP0144 — raw task coordination
        _ = new System.Threading.Tasks.TaskCompletionSource<int>();

        // TMP0145 — reflection / dynamic invocation
        _ = System.Activator.CreateInstance(typeof(int));

        // TMP0151 — unordered enumeration
        var map = new System.Collections.Generic.Dictionary<int, int>();
        foreach (var kv in map) { }

        // TMP0161 — culture-sensitive parse / format
        _ = long.Parse("42");
        _ = DateTimeOffset.Parse("2026-01-01");
        _ = string.Format("value={0}", 1);
    }
}
