using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Analyzers.Analyzers;

namespace Kogoshvili.Temporal.Analyzers.Tests;

public class ErrorHandlingAnalyzerTests
{
    private const string Stubs = TestStubs.Attributes + TestStubs.Sdk;

    private static Task Verify(string source)
    {
        var test = new CSharpAnalyzerTest<ErrorHandlingAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test.RunAsync();
    }

    [Fact]
    public Task ThrowsBaseException_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    {|TMP2132:throw|} new System.Exception("boom");
                }
            }
            """);

    [Fact]
    public Task ThrowsDerivedException_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    {|TMP2132:throw|} new System.InvalidOperationException("boom");
                }
            }
            """);

    [Fact]
    public Task ThrowsApplicationFailure_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    throw new Temporalio.Exceptions.ApplicationFailureException("boom");
                }
            }
            """);

    [Fact]
    public Task DebugAssertInWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    {|TMP2133:System.Diagnostics.Debug.Assert(true)|};
                }
            }
            """);

    [Fact]
    public Task DebugAssertOutsideWorkflow_DoesNotReport()
        => Verify(Stubs + """
            public static class Helper
            {
                public static void Check() { System.Diagnostics.Debug.Assert(true); }
            }
            """);
}
