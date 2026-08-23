using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Analyzers.Analyzers;

namespace Kogoshvili.Temporal.Analyzers.Tests;

public class SearchAttributeP2Tests
{
    private const string Stubs = TestStubs.Attributes + TestStubs.Sdk;

    private static Task Verify(string source)
    {
        var test = new CSharpAnalyzerTest<SearchAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test.RunAsync();
    }

    [Fact]
    public Task UpsertInsideLoop_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    for (var i = 0; i < 10; i++)
                    {
                        {|TMP2162:Temporalio.Workflows.Workflow.UpsertTypedSearchAttributes(
                            Temporalio.Workflows.SearchAttributeKey.ForKeyword("x").ValueSet(i))|};
                    }
                }
            }
            """);

    [Fact]
    public Task UpsertOutsideLoop_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    Temporalio.Workflows.Workflow.UpsertTypedSearchAttributes(
                        Temporalio.Workflows.SearchAttributeKey.ForKeyword("x").ValueSet(1));
                }
            }
            """);

    [Fact]
    public Task UnsetShapeWithNull_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    Temporalio.Workflows.Workflow.UpsertTypedSearchAttributes(
                        Temporalio.Workflows.SearchAttributeKey.ForKeyword("x").{|TMP2163:ValueSet|}(null));
                }
            }
            """);
}

public class VersioningP2Tests
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
    public Task DuplicatePatchId_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    if (Temporalio.Workflows.Workflow.Patched("p")) { } else { }
                    if ({|TMP3303:Temporalio.Workflows.Workflow.Patched("p")|}) { } else { }
                }
            }
            """);

    [Fact]
    public Task PatchedResultDiscarded_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    {|TMP3305:Temporalio.Workflows.Workflow.Patched("p")|};
                }
            }
            """);

    [Fact]
    public Task PatchWithElse_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    if (Temporalio.Workflows.Workflow.Patched("p")) { New(); } else { Old(); }
                }

                private void New() { }
                private void Old() { }
            }
            """);
}
