using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Analyzers.Analyzers;

namespace Kogoshvili.Temporal.Analyzers.Tests;

public class WorkflowLifecycleAnalyzerTests
{
    private const string Stubs = TestStubs.Attributes + TestStubs.Sdk;

    private static Task Verify(string source)
    {
        var test = new CSharpAnalyzerTest<WorkflowLifecycleAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test.RunAsync();
    }

    [Fact]
    public Task ContinueAsNewWithNullState_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    {|TMP2122:Temporalio.Workflows.Workflow.CreateContinueAsNewException(
                        "wf", null, new Temporalio.Workflows.ContinueAsNewOptions())|};
                }
            }
            """);

    [Fact]
    public Task ContinueAsNewWithState_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    Temporalio.Workflows.Workflow.CreateContinueAsNewException(
                        "wf", new object[] { 1 }, new Temporalio.Workflows.ContinueAsNewOptions());
                }
            }
            """);

    [Fact]
    public Task SwallowedCancellation_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    try { await System.Threading.Tasks.Task.Delay(1); }
                    {|TMP2123:catch|} (System.OperationCanceledException) { }
                }
            }
            """);

    [Fact]
    public Task RethrownCancellation_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    try { await System.Threading.Tasks.Task.Delay(1); }
                    catch (System.Exception) { throw; }
                }
            }
            """);

    [Fact]
    public Task CancellableCleanup_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    try { await System.Threading.Tasks.Task.Delay(1); }
                    {|TMP2124:finally|} { await System.Threading.Tasks.Task.Delay(1); }
                }
            }
            """);

    [Fact]
    public Task NonCancellableCleanup_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    try { await System.Threading.Tasks.Task.Delay(1); }
                    finally
                    {
                        await Temporalio.Workflows.Workflow.NonCancellableAsync(
                            async () => { await System.Threading.Tasks.Task.Delay(1); });
                    }
                }
            }
            """);

    [Fact]
    public Task UnboundedLoop_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    {|TMP2125:while|} (true) { await System.Threading.Tasks.Task.Delay(1); }
                }
            }
            """);

    [Fact]
    public Task LoopWithContinueAsNewCheck_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    while (true)
                    {
                        if (Temporalio.Workflows.Workflow.ContinueAsNewSuggested) { break; }
                        await System.Threading.Tasks.Task.Delay(1);
                    }
                }
            }
            """);
}
