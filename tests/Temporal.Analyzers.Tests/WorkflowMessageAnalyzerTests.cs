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
    public Task QueryMutatesNestedMember_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                private State _state = new State();

                [Temporalio.Workflows.WorkflowQuery]
                public int Get()
                {
                    {|TMP3206:_state.Count = 1|};
                    return _state.Count;
                }
            }

            public class State
            {
                public int Count;
            }
            """);

    [Fact]
    public Task QueryMutatesCollection_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                private System.Collections.Generic.List<int> _items = new();

                [Temporalio.Workflows.WorkflowQuery]
                public int Get()
                {
                    {|TMP3206:_items.Add(1)|};
                    return _items.Count;
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

    [Fact]
    public Task QueryCallsPureMethod_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                private Calculator _calc = new Calculator();

                [Temporalio.Workflows.WorkflowQuery]
                public int Get() => _calc.Add(1, 2);
            }

            public class Calculator
            {
                public int Add(int a, int b) => a + b;
            }
            """);

    [Fact]
    public Task PropertyQuery_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                private int _progress;

                [Temporalio.Workflows.WorkflowQuery]
                public int Progress => _progress;
            }
            """);

    [Fact]
    public Task SettablePropertyQuery_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowQuery]
                public int Progress { get; private set; }
            }
            """);

    [Fact]
    public Task TaskReturningPropertyQuery_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowQuery]
                public System.Threading.Tasks.Task {|TMP3204:Progress|} => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task PropertyQueryMutatesCollection_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                private System.Collections.Generic.List<int> _items = new();

                [Temporalio.Workflows.WorkflowQuery]
                public int Count
                {
                    get { {|TMP3206:_items.Add(1)|}; return _items.Count; }
                }
            }
            """);

    [Fact]
    public Task QueryReturnsObjectInitializer_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowQuery]
                public ProgressDto GetProgress() => new ProgressDto { Processed = 1, Failed = 2 };
            }

            public class ProgressDto
            {
                public int Processed { get; set; }
                public int Failed { get; set; }
            }
            """);

    [Fact]
    public Task QueryMutatesIndexer_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                private System.Collections.Generic.Dictionary<string, int> _dict = new();

                [Temporalio.Workflows.WorkflowQuery]
                public int Get()
                {
                    {|TMP3206:_dict["k"] = 1|};
                    return _dict.Count;
                }
            }
            """);
}
