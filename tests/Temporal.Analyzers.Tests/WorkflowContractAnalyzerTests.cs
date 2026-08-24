using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Analyzers.Analyzers;

namespace Kogoshvili.Temporal.Analyzers.Tests;

public class WorkflowContractAnalyzerTests
{
    private const string Stubs = TestStubs.Attributes;

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
    public Task NonPublicRunMethod_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                private System.Threading.Tasks.Task {|TMP3201:Run|}() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task NonTaskRunMethod_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public void {|TMP3201:Run|}() { }
            }
            """);

    [Fact]
    public Task RunMethodWithoutWorkflow_Reports()
        => Verify(Stubs + """
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task {|TMP3201:Run|}() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task MultipleRunMethods_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run1() => System.Threading.Tasks.Task.CompletedTask;

                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task {|TMP3201:Run2|}() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task ValidRunMethod_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task RunMethodOnWorkflowInterface_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public interface IW
            {
                [Temporalio.Workflows.WorkflowRun]
                System.Threading.Tasks.Task Run();
            }
            """);

    [Fact]
    public Task RunMethodOnUnattributedWorkflowInterface_DoesNotReport()
        => Verify(Stubs + """
            public interface IW
            {
                [Temporalio.Workflows.WorkflowRun]
                System.Threading.Tasks.Task Run();
            }

            [Temporalio.Workflows.Workflow]
            public class W : IW
            {
                public System.Threading.Tasks.Task Run() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task NonPublicActivity_DoesNotReport()
        => Verify(Stubs + """
            public static class Act
            {
                [Temporalio.Activities.Activity]
                internal static System.Threading.Tasks.Task Do() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task SynchronousActivity_DoesNotReport()
        => Verify(Stubs + """
            public static class Act
            {
                [Temporalio.Activities.Activity]
                public static int AddOne(int num) => num + 1;
            }
            """);

    [Fact]
    public Task ActivityOnClass_Reports()
        => Verify(Stubs + """
            [Temporalio.Activities.Activity]
            public class {|TMP3202:Act|} { }
            """);

    [Fact]
    public Task MissingActivityOnTypedLambda_Reports()
        => Verify(TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var opts = new Temporalio.Workflows.ActivityOptions();
                    {|TMP3202:Temporalio.Workflows.Workflow.ExecuteActivityAsync(() => DoThing(), opts)|};
                }

                public static System.Threading.Tasks.Task DoThing() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task TypedLambdaWithActivity_DoesNotReport()
        => Verify(TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var opts = new Temporalio.Workflows.ActivityOptions();
                    await Temporalio.Workflows.Workflow.ExecuteActivityAsync(() => Act.Do(), opts);
                }
            }

            public static class Act
            {
                [Temporalio.Activities.Activity]
                public static System.Threading.Tasks.Task Do() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task ConstructorSchedulesBlockingCommand_Reports()
        => Verify(TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowInit]
                public W()
                {
                    {|TMP3210:Temporalio.Workflows.Workflow.ExecuteActivityAsync(
                        "a",
                        new object[] { },
                        new Temporalio.Workflows.ActivityOptions())|};
                }

                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task ConstructorSchedulesDelay_Reports()
        => Verify(TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                public W()
                {
                    {|TMP3210:Temporalio.Workflows.Workflow.DelayAsync(100)|};
                }

                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task ConstructorWithoutBlockingCommand_DoesNotReport()
        => Verify(TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                public W()
                {
                    var x = 1;
                }

                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task NonWorkflowConstructorCallingWorkflowApi_DoesNotReport()
        => Verify(TestStubs.Attributes + TestStubs.Sdk + """
            public class Plain
            {
                public Plain()
                {
                    Temporalio.Workflows.Workflow.DelayAsync(100);
                }
            }
            """);
}
