using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Analyzers.Analyzers;

namespace Kogoshvili.Temporal.Analyzers.Tests;

public class TestingAnalyzerTests
{
    private const string Stubs = TestStubs.Attributes + TestStubs.Sdk + TestStubs.Testing;

    // The three Testing rules share preconditions (a WorkflowEnvironment local),
    // so a single source often triggers more than one. The analyzer-testing
    // framework enables opt-in rules at their default severity; silence the two
    // rules not under test so each test exercises exactly one rule.
    private static Task Verify(string source, string enabledRule)
    {
        var test = new CSharpAnalyzerTest<TestingAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

        foreach (var rule in new[] { "TMP5001", "TMP5002", "TMP5003" })
        {
            if (rule != enabledRule)
            {
                test.DisabledDiagnostics.Add(rule);
            }
        }

        return test.RunAsync();
    }

    private const string WorkflowStub = """
        [Temporalio.Workflows.Workflow]
        public class MyWorkflow
        {
            [Temporalio.Workflows.WorkflowRun]
            public System.Threading.Tasks.Task Run() => System.Threading.Tasks.Task.CompletedTask;
        }
        """;

    [Fact]
    public Task Workflow_NoReplayer_Reports()
        => Verify(
            Stubs + """
                [Temporalio.Workflows.Workflow]
                public class {|TMP5001:MyWorkflow|}
                {
                    [Temporalio.Workflows.WorkflowRun]
                    public System.Threading.Tasks.Task Run() => System.Threading.Tasks.Task.CompletedTask;
                }
                """,
            "TMP5001");

    [Fact]
    public Task Workflow_WithReplayer_DoesNotReport()
        => Verify(
            Stubs + WorkflowStub + """

                public class ReplayTests
                {
                    public void Test() => _ = new Temporalio.Worker.WorkflowReplayer(null);
                }
                """,
            "TMP5001");

    [Fact]
    public Task Environment_NotDisposed_Reports()
        => Verify(
            Stubs + """
                public class EnvTests
                {
                    public async System.Threading.Tasks.Task Test()
                    {
                        var {|TMP5002:env|} = await Temporalio.Testing.WorkflowEnvironment.StartLocalAsync();
                    }
                }
                """,
            "TMP5002");

    [Fact]
    public Task Environment_AwaitUsing_DoesNotReport()
        => Verify(
            Stubs + """
                public class EnvTests
                {
                    public async System.Threading.Tasks.Task Test()
                    {
                        await using var env = await Temporalio.Testing.WorkflowEnvironment.StartLocalAsync();
                    }
                }
                """,
            "TMP5002");

    [Fact]
    public Task Environment_ExplicitDispose_DoesNotReport()
        => Verify(
            Stubs + """
                public class EnvTests
                {
                    public async System.Threading.Tasks.Task Test()
                    {
                        var env = await Temporalio.Testing.WorkflowEnvironment.StartLocalAsync();
                        await env.DisposeAsync();
                    }
                }
                """,
            "TMP5002");

    [Fact]
    public Task Environment_NoWorkerExecuteAsync_Reports()
        => Verify(
            Stubs + """
                public class EnvTests
                {
                    public async System.Threading.Tasks.Task Test()
                    {
                        await using var {|TMP5003:env|} = await Temporalio.Testing.WorkflowEnvironment.StartLocalAsync();
                    }
                }
                """,
            "TMP5003");

    [Fact]
    public Task Environment_WithWorkerExecuteAsync_DoesNotReport()
        => Verify(
            Stubs + """
                public class EnvTests
                {
                    public async System.Threading.Tasks.Task Test()
                    {
                        await using var env = await Temporalio.Testing.WorkflowEnvironment.StartLocalAsync();
                        var worker = new Temporalio.Worker.TemporalWorker(
                            env.Client,
                            new Temporalio.Worker.TemporalWorkerOptions("test-queue"));
                        await worker.ExecuteAsync();
                    }
                }
                """,
            "TMP5003");
}
