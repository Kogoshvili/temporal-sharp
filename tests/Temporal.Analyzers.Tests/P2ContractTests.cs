using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Analyzers.Analyzers;

namespace Kogoshvili.Temporal.Analyzers.Tests;

public class WorkflowContractP2Tests
{
    private const string Stubs = TestStubs.Attributes + TestStubs.Sdk;

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
    public Task MixedWorkflowAndActivity_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class {|TMP3214:W|}
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run() => System.Threading.Tasks.Task.CompletedTask;

                [Temporalio.Activities.Activity]
                public System.Threading.Tasks.Task DoWork() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task ParameterizedCtorWithoutInit_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                public {|TMP3219:W|}(int x) { }

                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task InitAndRunParameterMismatch_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowInit]
                public W(int x) { }

                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task {|TMP3218:Run|}() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task InitAndRunParameterMatch_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowInit]
                public W(int x) { }

                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run(int x) => System.Threading.Tasks.Task.CompletedTask;
            }
            """);
}

public class WorkflowMessageP2Tests
{
    private const string Stubs = TestStubs.Attributes + TestStubs.Sdk;

    private static Task Verify(string source)
    {
        var test = new CSharpAnalyzerTest<WorkflowMessageAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test.RunAsync();
    }

    [Fact]
    public Task MessageNameNotLiteral_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                public const string QUERY_NAME = "my-query";

                [Temporalio.Workflows.WorkflowQuery(Name = {|TMP3211:QUERY_NAME|})]
                public string Get() => "ok";
            }
            """);

    [Fact]
    public Task MessageNameNameof_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowQuery(Name = nameof(W))]
                public string Get() => "ok";
            }
            """);

    [Fact]
    public Task MessageNameLiteral_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowQuery(Name = "my-query")]
                public string Get() => "ok";
            }
            """);
}
