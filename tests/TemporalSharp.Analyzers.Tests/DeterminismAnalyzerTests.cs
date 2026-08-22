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
}
