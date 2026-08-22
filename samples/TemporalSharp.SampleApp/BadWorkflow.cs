using Temporalio.Workflows;

namespace TemporalSharp.SampleApp;

// This workflow contains intentional violations that the analyzer should flag.
[Workflow]
public class BadWorkflow
{
    private static int sharedCounter;

    [WorkflowRun]
    public async Task RunAsync()
    {
        var now = DateTime.Now;                              // TMP0101 wall-clock time
        var id = Guid.NewGuid();                              // TMP0121 randomness
        await Task.Delay(100);                                // TMP0111 sleep/block
        sharedCounter++;                                      // TMP1101 static-state mutation
        Console.WriteLine("started");                         // TMP2131 non-replay-aware logging
        var opts = new ActivityOptions { TaskQueue = "q" };    // TMP2101 missing timeout
        await Workflow.ExecuteActivityAsync("Greet", null, opts); // TMP2111 string target

        _ = Task.Run(() => { });                              // TMP0141 concurrency
        lock (this) { }                                       // TMP0142 blocking primitive
        var map = new System.Collections.Generic.Dictionary<int, int>();
        foreach (var kv in map) { }                           // TMP0151 unordered enumeration
    }
}
