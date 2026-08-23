using Temporalio.Workflows;

namespace TemporalSharp.SampleApp;

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

        // TMP0141 — concurrency
        _ = System.Threading.Tasks.Task.Run(() => { });

        // TMP0142 — blocking synchronization primitive
        lock (this) { }

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
