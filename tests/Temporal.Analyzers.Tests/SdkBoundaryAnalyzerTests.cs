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
    public Task StartWorkflowWithParameterOptions_DoesNotReport()
        => Verify(Stubs + """
            public class C
            {
                public async System.Threading.Tasks.Task Start(Temporalio.Client.WorkflowOptions options)
                {
                    var client = new Temporalio.Client.WorkflowClient();
                    await client.StartWorkflowAsync(() => new object(), options);
                }
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
    public Task ExecuteWorkflowWithoutId_Reports()
        => Verify(Stubs + """
            public class C
            {
                public async System.Threading.Tasks.Task Start()
                {
                    var client = new Temporalio.Client.WorkflowClient();
                    await {|TMP3213:client.ExecuteWorkflowAsync(
                        () => new object(), new Temporalio.Client.WorkflowOptions())|};
                }
            }
            """);

    [Fact]
    public Task ExecuteWorkflowWithId_DoesNotReport()
        => Verify(Stubs + """
            public class C
            {
                public async System.Threading.Tasks.Task Start()
                {
                    var client = new Temporalio.Client.WorkflowClient();
                    await client.ExecuteWorkflowAsync(
                        () => new object(), new Temporalio.Client.WorkflowOptions { Id = "my-id" });
                }
            }
            """);

    [Fact]
    public Task StartWorkflowWithConstructorId_DoesNotReport()
        => Verify(Stubs + """
            public class C
            {
                public async System.Threading.Tasks.Task Start()
                {
                    var client = new Temporalio.Client.WorkflowClient();
                    await client.StartWorkflowAsync(
                        () => new object(), new Temporalio.Client.WorkflowOptions(id: "my-id", taskQueue: "q"));
                }
            }
            """);

    [Fact]
    public Task StartWorkflowWithPositionalId_DoesNotReport()
        => Verify(Stubs + """
            public class C
            {
                public async System.Threading.Tasks.Task Start()
                {
                    var client = new Temporalio.Client.WorkflowClient();
                    await client.StartWorkflowAsync(
                        () => new object(), new Temporalio.Client.WorkflowOptions("my-id", "q"));
                }
            }
            """);

    [Fact]
    public Task StartWorkflowWithIdViaVariable_DoesNotReport()
        => Verify(Stubs + """
            public class C
            {
                public async System.Threading.Tasks.Task Start()
                {
                    var client = new Temporalio.Client.WorkflowClient();
                    var options = new Temporalio.Client.WorkflowOptions { Id = "my-id" };
                    await client.StartWorkflowAsync(() => new object(), options);
                }
            }
            """);

    [Fact]
    public Task UserMethodNamedStartWorkflowAsync_DoesNotReport()
        => Verify(Stubs + """
            public class C
            {
                public async System.Threading.Tasks.Task Start()
                {
                    await StartWorkflowAsync();
                }

                public System.Threading.Tasks.Task StartWorkflowAsync() => System.Threading.Tasks.Task.CompletedTask;
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
}
