using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using TemporalSharp.Analyzers.Analyzers;

namespace TemporalSharp.Analyzers.Tests;

public class DeterminismAnalyzerTests
{
    private const string Stubs = TestStubs.Attributes;

    private static Task Verify(string source)
    {
        var test = new CSharpAnalyzerTest<DeterminismAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test.RunAsync();
    }

    [Fact]
    public Task DateTimeNow_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var now = {|TMP0101:System.DateTime.Now|};
                }
            }
            """);

    [Fact]
    public Task TaskDelay_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP0111:System.Threading.Tasks.Task.Delay(100)|};
                }
            }
            """);

    [Fact]
    public Task GuidNewGuid_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var g = {|TMP0121:System.Guid.NewGuid()|};
                }
            }
            """);

    [Fact]
    public Task ParameterlessRandom_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var r = {|TMP0121:new System.Random()|};
                }
            }
            """);

    [Fact]
    public Task SeededRandom_InWorkflow_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var r = new System.Random(42);
                }
            }
            """);

    [Fact]
    public Task RandomShared_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var r = {|TMP0121:System.Random.Shared|};
                }
            }
            """);

    [Fact]
    public Task EnvironmentAccess_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var v = {|TMP0131:System.Environment.GetEnvironmentVariable("X")|};
                }
            }
            """);

    [Fact]
    public Task TransitiveHelperCall_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    Helper.MakeGuid();
                }
            }

            public static class Helper
            {
                public static void MakeGuid()
                {
                    var g = {|TMP0121:System.Guid.NewGuid()|};
                }
            }
            """);

    [Fact]
    public Task NonWorkflowCode_DoesNotReport()
        => Verify(Stubs + """
            public class PlainClass
            {
                public void DoSomething()
                {
                    var now = System.DateTime.Now;
                    var g = System.Guid.NewGuid();
                    System.Console.WriteLine("x");
                }
            }
            """);

    [Fact]
    public Task ActivityMethod_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    Activities.DoWork();
                }
            }

            public static class Activities
            {
                [Temporalio.Activities.Activity]
                public static void DoWork()
                {
                    var g = System.Guid.NewGuid();
                }
            }
            """);

    [Fact]
    public Task TaskRun_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP0141:System.Threading.Tasks.Task.Run(() => { })|};
                }
            }
            """);

    [Fact]
    public Task NewThread_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP0141:new System.Threading.Thread(() => { })|};
                }
            }
            """);

    [Fact]
    public Task SemaphoreSlimWait_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var sem = new System.Threading.SemaphoreSlim(1);
                    {|TMP0142:sem.Wait()|};
                }
            }
            """);

    [Fact]
    public Task LockStatement_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP0142:lock|} (this) { }
                }
            }
            """);

    [Fact]
    public Task ForeachDictionary_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var d = new System.Collections.Generic.Dictionary<int, int>();
                    {|TMP0151:foreach|} (var kv in d) { }
                }
            }
            """);

    [Fact]
    public Task ForeachOrderedList_InWorkflow_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var list = new System.Collections.Generic.List<int>();
                    foreach (var x in list) { }
                }
            }
            """);

    [Fact]
    public Task ForeachOrderByDictionary_InWorkflow_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var d = new System.Collections.Generic.Dictionary<int, int>();
                    foreach (var kv in System.Linq.Enumerable.OrderBy(d, x => x.Key)) { }
                }
            }
            """);

    [Fact]
    public Task ProcessStart_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP0131:System.Diagnostics.Process.Start("cmd")|};
                }
            }
            """);
}
