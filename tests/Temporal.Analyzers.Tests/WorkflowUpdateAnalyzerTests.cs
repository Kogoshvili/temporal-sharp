using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Analyzers.Analyzers;

namespace Kogoshvili.Temporal.Analyzers.Tests;

public class WorkflowUpdateAnalyzerTests
{
    private const string Stubs = TestStubs.Attributes + TestStubs.Sdk;

    private static Task Verify(string source)
    {
        var test = new CSharpAnalyzerTest<WorkflowUpdateAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test.RunAsync();
    }

    [Fact]
    public Task UpdateReturningVoid_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowUpdate]
                public void {|TMP3208:Update|}(int x) { }
            }
            """);

    [Fact]
    public Task UpdateReturningNonGenericTask_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowUpdate]
                public System.Threading.Tasks.Task Update(int x)
                    => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task UpdateReturningTaskOfT_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowUpdate]
                public System.Threading.Tasks.Task<int> Update(int x) => System.Threading.Tasks.Task.FromResult(1);
            }
            """);

    [Fact]
    public Task ContinueAsNewInUpdate_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowUpdate]
                public async System.Threading.Tasks.Task<int> Update(int x)
                {
                    {|TMP3209:Temporalio.Workflows.Workflow.CreateContinueAsNewException(
                        "wf", null, new Temporalio.Workflows.ContinueAsNewOptions())|};
                    return 1;
                }
            }
            """);

    [Fact]
    public Task ValidatorMutatesState_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                private int _count;

                [Temporalio.Workflows.WorkflowUpdateValidator]
                public void Validate(int x)
                {
                    {|TMP3215:_count = x|};
                }
            }
            """);

    [Fact]
    public Task ValidatorMutatesCollection_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                private System.Collections.Generic.List<int> _items = new();

                [Temporalio.Workflows.WorkflowUpdateValidator]
                public void Validate(int x)
                {
                    {|TMP3215:_items.Add(x)|};
                }
            }
            """);

    [Fact]
    public Task ValidatorSchedulesCommand_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowUpdateValidator]
                public void Validate(int x)
                {
                    {|TMP3215:Temporalio.Workflows.Workflow.DelayAsync(100)|};
                }
            }
            """);

    [Fact]
    public Task ValidatorCallsPureMethod_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                private Calculator _calc = new Calculator();

                [Temporalio.Workflows.WorkflowUpdateValidator]
                public void Validate(int x)
                {
                    _ = _calc.Add(x, 1);
                }
            }

            public class Calculator
            {
                public int Add(int a, int b) => a + b;
            }
            """);

    [Fact]
    public Task ValidatorMutatesIndexer_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                private System.Collections.Generic.Dictionary<string, int> _dict = new();

                [Temporalio.Workflows.WorkflowUpdateValidator]
                public void Validate(int x)
                {
                    {|TMP3215:_dict["k"] = x|};
                }
            }
            """);

    [Fact]
    public Task ValidatorObjectInitializer_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowUpdateValidator]
                public void Validate(int x)
                {
                    var dto = new Dto { Value = x };
                }
            }

            public class Dto
            {
                public int Value { get; set; }
            }
            """);

    [Fact]
    public Task SignalHandlerSchedulesCommand_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowSignal]
                public void Handle()
                {
                    {|TMP3216:Temporalio.Workflows.Workflow.DelayAsync(100)|};
                }
            }
            """);

    [Fact]
    public Task UpdateHandlerSchedulesCommand_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowUpdate]
                public async System.Threading.Tasks.Task<int> Update(int x)
                {
                    {|TMP3216:Temporalio.Workflows.Workflow.DelayAsync(100)|};
                    return 1;
                }
            }
            """);

    [Fact]
    public Task AsyncHandlerWithoutAllHandlersFinished_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class {|TMP3217:W|}
            {
                [Temporalio.Workflows.WorkflowSignal]
                public async System.Threading.Tasks.Task Handle()
                {
                    await System.Threading.Tasks.Task.Delay(1);
                }

                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    await System.Threading.Tasks.Task.Delay(1);
                }
            }
            """);

    [Fact]
    public Task AsyncHandlerWithAllHandlersFinished_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowSignal]
                public async System.Threading.Tasks.Task Handle()
                {
                    await System.Threading.Tasks.Task.Delay(1);
                }

                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    await Temporalio.Workflows.Workflow.AllHandlersFinished;
                }
            }
            """);
}
