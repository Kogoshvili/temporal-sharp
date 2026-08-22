using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using TemporalSharp.Analyzers.Analyzers;

namespace TemporalSharp.Analyzers.Tests;

public class VersioningAnalyzerTests
{
    private const string Stubs = TestStubs.Attributes + TestStubs.Sdk;

    private static Task Verify(string source)
    {
        var test = new CSharpAnalyzerTest<VersioningAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test.RunAsync();
    }

    [Fact]
    public Task NonConstantPatchId_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var id = "my-patch";
                    {|TMP3301:Temporalio.Workflows.Workflow.Patched(id)|};
                }
            }
            """);

    [Fact]
    public Task ConstantPatchId_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    if (Temporalio.Workflows.Workflow.Patched("my-patch")) { }
                }
            }
            """);

    [Fact]
    public Task LeftoverPatchedAndDeprecated_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    if (Temporalio.Workflows.Workflow.Patched("my-patch")) { }
                    {|TMP3301:Temporalio.Workflows.Workflow.DeprecatePatch("my-patch")|};
                }
            }
            """);

    [Fact]
    public Task DeprecateOnly_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    Temporalio.Workflows.Workflow.DeprecatePatch("my-patch");
                }
            }
            """);
}
