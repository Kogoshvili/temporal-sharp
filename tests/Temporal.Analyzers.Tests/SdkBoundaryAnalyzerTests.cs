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
    public Task WorkflowHandleInWorkflow_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    Temporalio.Client.WorkflowHandle handle = null;
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

    [Fact]
    public Task FullyQualifiedInternalNamespace_Reports()
    {
        var test = new CSharpAnalyzerTest<SdkBoundaryAnalyzer, DefaultVerifier>
        {
            TestCode = """
                namespace Temporalio.Bridge.Api { public class X { } }

                public class C
                {
                    public void M()
                    {
                        {|TMP2146:Temporalio.Bridge.Api.X|} x = default;
                    }
                }
                """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test.RunAsync();
    }

    [Fact]
    public Task PublicTemporalNamespaces_DoNotReport()
    {
        var test = new CSharpAnalyzerTest<SdkBoundaryAnalyzer, DefaultVerifier>
        {
            TestCode = """
                using Temporalio.Api;
                using Temporalio.Api.Enums.V1;
                using Temporalio.Worker.Interceptors;
                using Temporalio.Runtime;
                using Temporalio.Converters;

                namespace Temporalio.Api { }
                namespace Temporalio.Api.Enums.V1 { }
                namespace Temporalio.Worker.Interceptors { }
                namespace Temporalio.Runtime { }
                namespace Temporalio.Converters { }

                public class C { }
                """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test.RunAsync();
    }

    [Fact]
    public Task BridgePrefixedNamespace_DoesNotReport()
    {
        var test = new CSharpAnalyzerTest<SdkBoundaryAnalyzer, DefaultVerifier>
        {
            TestCode = """
                using Temporalio.BridgeSomething;

                namespace Temporalio.BridgeSomething { }

                public class C { }
                """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test.RunAsync();
    }

    [Fact]
    public Task StandaloneActivityInWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    Temporalio.Client.{|TMP3212:TemporalClient|} client = null;
                    await {|TMP3213:client.ExecuteActivityAsync("a", null, null)|};
                }
            }
            """);

    [Fact]
    public Task StandaloneActivityStartInWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    Temporalio.Client.{|TMP3212:TemporalClient|} client = null;
                    await {|TMP3213:client.StartActivityAsync("a", null, null)|};
                }
            }
            """);

    [Fact]
    public Task StandaloneActivityOutsideWorkflow_DoesNotReport()
        => Verify(Stubs + """
            public class Starter
            {
                public async System.Threading.Tasks.Task Start()
                {
                    var client = new Temporalio.Client.TemporalClient();
                    await client.ExecuteActivityAsync("a", null, null);
                }
            }
            """);

    [Fact]
    public Task WorkflowUnsafeIsReplayingInWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    if (Temporalio.Workflows.Workflow.Unsafe.{|TMP2148:IsReplaying|})
                    {
                        Temporalio.Workflows.Workflow.Logger.LogInformation("replaying");
                    }
                }
            }
            """);

    [Fact]
    public Task WorkflowUnsafeUsingStatic_Reports()
        => Verify("""
            using static Temporalio.Workflows.Workflow.Unsafe;

            """ + Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var r = {|TMP2148:IsReplayingHistoryEvents|};
                }
            }
            """);

    [Fact]
    public Task WorkflowUnsafeOutsideWorkflow_DoesNotReport()
        => Verify(Stubs + """
            public class Plain
            {
                public bool Check() => Temporalio.Workflows.Workflow.Unsafe.IsReplaying;
            }
            """);
}
