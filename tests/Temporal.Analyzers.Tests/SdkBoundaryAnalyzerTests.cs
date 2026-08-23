using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Analyzers.Analyzers;

namespace Kogoshvili.Temporal.Analyzers.Tests;

public class SdkBoundaryAnalyzerTests
{
    private const string Stubs = TestStubs.Attributes + TestStubs.Sdk;

    private static Task Verify(string source)
    {
        var test = new CSharpAnalyzerTest<SdkBoundaryAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test.RunAsync();
    }

    [Fact]
    public Task ClientTypeInWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    Temporalio.Client.{|TMP3212:TemporalClient|} client = null;
                }
            }
            """);

    [Fact]
    public Task ClientTypeOutsideWorkflow_DoesNotReport()
        => Verify(Stubs + """
            public class Plain
            {
                public Temporalio.Client.TemporalClient Client = null;
            }
            """);

    [Fact]
    public Task StartWorkflowWithoutId_Reports()
        => Verify(Stubs + """
            public class C
            {
                public async System.Threading.Tasks.Task Start()
                {
                    var client = new Temporalio.Client.WorkflowClient();
                    await {|TMP3213:client.StartWorkflowAsync(
                        () => new object(), new Temporalio.Client.WorkflowOptions())|};
                }
            }
            """);

    [Fact]
    public Task StartWorkflowWithId_DoesNotReport()
        => Verify(Stubs + """
            public class C
            {
                public async System.Threading.Tasks.Task Start()
                {
                    var client = new Temporalio.Client.WorkflowClient();
                    await client.StartWorkflowAsync(
                        () => new object(), new Temporalio.Client.WorkflowOptions { Id = "my-id" });
                }
            }
            """);

    [Fact]
    public Task InternalTemporalNamespace_Reports()
    {
        var test = new CSharpAnalyzerTest<SdkBoundaryAnalyzer, DefaultVerifier>
        {
            TestCode = """
                using {|TMP2146:Temporalio.Bridge|};

                namespace Temporalio.Bridge { }

                public class C { }
                """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test.RunAsync();
    }
}
