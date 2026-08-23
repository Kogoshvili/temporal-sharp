using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Analyzers.Analyzers;

namespace Kogoshvili.Temporal.Analyzers.Tests;

public class WorkflowMessageAnalyzerTests
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
    public Task AsyncQuery_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowQuery]
                public async System.Threading.Tasks.Task<int> {|TMP3204:Get|}() => 1;
            }
            """);

    [Fact]
    public Task VoidQuery_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowQuery]
                public void {|TMP3204:Get|}() { }
            }
            """);

    [Fact]
    public Task TaskReturningQuery_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowQuery]
                public System.Threading.Tasks.Task {|TMP3204:Get|}() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task ValueQuery_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowQuery]
                public string Get() => "ok";
            }
            """);

    [Fact]
    public Task SignalReturningValue_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowSignal]
                public int {|TMP3205:Handle|}() => 1;
            }
            """);

    [Fact]
    public Task SignalReturningTaskOfT_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowSignal]
                public System.Threading.Tasks.Task<int> {|TMP3205:Handle|}() => System.Threading.Tasks.Task.FromResult(1);
            }
            """);

    [Fact]
    public Task SignalReturningVoid_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowSignal]
                public void Handle() { }
            }
            """);

    [Fact]
    public Task SignalReturningTask_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowSignal]
                public System.Threading.Tasks.Task Handle() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task QueryMutatesField_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                private int _value;

                [Temporalio.Workflows.WorkflowQuery]
                public int Get()
                {
                    {|TMP3206:_value = 1|};
                    return _value;
                }
            }
            """);

    [Fact]
    public Task QueryMutatesProperty_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                private int Value { get; set; }

                [Temporalio.Workflows.WorkflowQuery]
                public int Get()
                {
                    {|TMP3206:Value = 1|};
                    return Value;
                }
            }
            """);

    [Fact]
    public Task QueryReadsField_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                private int _value = 42;

                [Temporalio.Workflows.WorkflowQuery]
                public int Get() => _value;
            }
            """);

    [Fact]
    public Task QueryCallsWorkflowApi_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowQuery]
                public string Get()
                {
                    {|TMP3207:Temporalio.Workflows.Workflow.DelayAsync(100)|};
                    return "ok";
                }
            }
            """);

    [Fact]
    public Task SignalCallsWorkflowApi_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowSignal]
                public void Handle()
                {
                    Temporalio.Workflows.Workflow.DelayAsync(100);
                }
            }
            """);
}
