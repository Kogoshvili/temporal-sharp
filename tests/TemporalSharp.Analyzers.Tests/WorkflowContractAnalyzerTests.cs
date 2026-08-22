using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using TemporalSharp.Analyzers.Analyzers;

namespace TemporalSharp.Analyzers.Tests;

public class WorkflowContractAnalyzerTests
{
    private const string Stubs = TestStubs.Attributes;

    private static Task Verify(string source)
    {
        var test = new CSharpAnalyzerTest<WorkflowContractAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test.RunAsync();
    }

    [Fact]
    public Task NonPublicRunMethod_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                private System.Threading.Tasks.Task {|TMP3201:Run|}() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task NonTaskRunMethod_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public void {|TMP3201:Run|}() { }
            }
            """);

    [Fact]
    public Task RunMethodWithoutWorkflow_Reports()
        => Verify(Stubs + """
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task {|TMP3201:Run|}() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task MultipleRunMethods_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run1() => System.Threading.Tasks.Task.CompletedTask;

                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task {|TMP3201:Run2|}() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task ValidRunMethod_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);
}
