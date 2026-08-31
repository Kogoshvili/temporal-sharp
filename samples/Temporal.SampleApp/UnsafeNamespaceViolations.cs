using System.IO;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.SampleApp;

// TMP2147 (opt-in) — unsafe namespace imported into workflow code. The
// System.IO prefix is configured via kogoshvili.temporal.unsafe_namespaces in
// this project's .editorconfig.
[Workflow]
public class UnsafeNamespaceViolations
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        _ = File.ReadAllText("/tmp/unused");
    }
}
