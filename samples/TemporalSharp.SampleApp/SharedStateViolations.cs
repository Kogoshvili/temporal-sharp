using System.Threading;
using Temporalio.Workflows;

namespace TemporalSharp.SampleApp;

// Shared-state mutation (TMP11xx). Workflow state must stay instance-local;
// mutating static state breaks replay determinism and races across executions.
[Workflow]
public class SharedStateViolations
{
    private static int counter;
    private static string? label;

    [ThreadStatic]
    private static int threadScoped;

    private static readonly System.Collections.Generic.List<int> Items = new();
    private static readonly System.Text.StringBuilder Builder = new();

    private static string? Name { get; set; }

    [WorkflowRun]
    public async Task RunAsync()
    {
        counter++;                       // TMP1101 — static field mutation
        label = "x";                     // TMP1101 — static field assignment

        Name = "x";                      // TMP1103 — static property setter

        threadScoped = 1;                // TMP1102 — [ThreadStatic] mutation

        Items.Add(1);                    // TMP1104 — static collection mutation
        Builder.Append("x");             // TMP1105 — static reference mutated via method call

        var local = new AsyncLocal<int>();   // TMP1106 — ambient state creation
        _ = local.Value;                     // TMP1106 — ambient state value access
    }
}
