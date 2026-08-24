using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Analyzers.Analyzers;

namespace Kogoshvili.Temporal.Analyzers.Tests;

public class BestPracticeAnalyzerTests
{
    private const string Stubs = TestStubs.Attributes + TestStubs.Sdk;

    private static Task Verify(string source)
    {
        var test = new CSharpAnalyzerTest<BestPracticeAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test.RunAsync();
    }

    private static Task VerifyWithConfig(string source, string editorConfig)
    {
        var test = new CSharpAnalyzerTest<BestPracticeAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", editorConfig));
        return test.RunAsync();
    }

    private const string OptionsStub = """
        public static class A
        {
            [Temporalio.Activities.Activity]
            public static System.Threading.Tasks.Task First() => System.Threading.Tasks.Task.CompletedTask;

            [Temporalio.Activities.Activity]
            public static System.Threading.Tasks.Task Second() => System.Threading.Tasks.Task.CompletedTask;
        }
        """;

    [Fact]
    public Task WorkflowRun_MultipleParameters_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task {|TMP4101:Run|}(string a, int b, string c, long d)
                    => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task WorkflowRun_TwoParameters_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run(string a, int b)
                    => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task WorkflowRun_SingleParameter_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run(string a) => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task WorkflowRun_CancellationTokenIgnored_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run(string a, System.Threading.CancellationToken ct)
                    => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task Activity_MultipleParameters_Reports()
        => Verify(Stubs + """
            public class A
            {
                [Temporalio.Activities.Activity]
                public System.Threading.Tasks.Task {|TMP4101:Process|}(string orderId, int amount, string c, long d)
                    => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task PollingLoop_ConstantDelay_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                private bool _done;

                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    {|TMP4103:while|} (!_done)
                    {
                        await Temporalio.Workflows.Workflow.DelayAsync(100);
                    }
                }
            }
            """);

    [Fact]
    public Task PeriodicLoop_ConstantCondition_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    while (true)
                    {
                        await Temporalio.Workflows.Workflow.DelayAsync(100);
                    }
                }
            }
            """);

    [Fact]
    public Task PollingLoop_VariableDelay_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run(int delay)
                {
                    while (true)
                    {
                        await Temporalio.Workflows.Workflow.DelayAsync(delay);
                    }
                }
            }
            """);

    [Fact]
    public Task TaskQueue_SingleOccurrence_DoesNotReport()
        => Verify(Stubs + """
            public class C
            {
                public void Start()
                {
                    var options = new Temporalio.Client.WorkflowOptions { TaskQueue = "my-queue" };
                }
            }
            """);

    [Fact]
    public Task TaskQueue_DuplicatedAcrossSites_Reports()
        => Verify(Stubs + """
            public class C
            {
                public void Start()
                {
                    var a = new Temporalio.Client.WorkflowOptions { {|TMP4105:TaskQueue = "my-queue"|} };
                    var b = new Temporalio.Client.WorkflowOptions { TaskQueue = "my-queue" };
                }
            }
            """);

    [Fact]
    public Task TaskQueue_Constant_DoesNotReport()
        => Verify(Stubs + """
            public static class Queues
            {
                public const string Main = "main";
            }

            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    await Temporalio.Workflows.Workflow.ExecuteActivityAsync(
                        "a",
                        new object[] { },
                        new Temporalio.Workflows.ActivityOptions
                        {
                            StartToCloseTimeout = System.TimeSpan.FromMinutes(1),
                            TaskQueue = Queues.Main,
                        });
                }
            }
            """);

    [Fact]
    public Task TaskQueue_WorkerOptionsConstructor_Duplicated_Reports()
        => Verify(Stubs + """
            public class C
            {
                public void Start()
                {
                    var a = new Temporalio.Worker.TemporalWorkerOptions({|TMP4105:"my-queue"|});
                    var b = new Temporalio.Worker.TemporalWorkerOptions("my-queue");
                }
            }
            """);

    [Fact]
    public Task TaskQueue_WorkflowOptionsNamedCtorArg_Duplicated_Reports()
        => Verify(Stubs + """
            public class C
            {
                public void Start()
                {
                    var a = new Temporalio.Client.WorkflowOptions({|TMP4105:taskQueue: "my-queue"|});
                    var b = new Temporalio.Client.WorkflowOptions(taskQueue: "my-queue");
                }
            }
            """);

    [Fact]
    public Task TaskQueue_ImplicitNew_Duplicated_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    Temporalio.Workflows.ActivityOptions a = new() { {|TMP4105:TaskQueue = "my-queue"|} };
                    Temporalio.Workflows.ActivityOptions b = new() { TaskQueue = "my-queue" };
                }
            }
            """);

    [Fact]
    public Task ConsecutiveLocalActivities_Reports()
        => Verify(Stubs + OptionsStub + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    await Temporalio.Workflows.Workflow.ExecuteLocalActivityAsync(
                        () => A.First(),
                        new Temporalio.Workflows.LocalActivityOptions { StartToCloseTimeout = System.TimeSpan.FromMinutes(1) });
                    await {|TMP4106:Temporalio.Workflows.Workflow.ExecuteLocalActivityAsync(
                        () => A.Second(),
                        new Temporalio.Workflows.LocalActivityOptions { StartToCloseTimeout = System.TimeSpan.FromMinutes(1) })|};
                }
            }
            """);

    [Fact]
    public Task LocalActivities_SeparatedByCommand_DoesNotReport()
        => Verify(Stubs + OptionsStub + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    await Temporalio.Workflows.Workflow.ExecuteLocalActivityAsync(
                        () => A.First(),
                        new Temporalio.Workflows.LocalActivityOptions { StartToCloseTimeout = System.TimeSpan.FromMinutes(1) });
                    await Temporalio.Workflows.Workflow.DelayAsync(100);
                    await Temporalio.Workflows.Workflow.ExecuteLocalActivityAsync(
                        () => A.Second(),
                        new Temporalio.Workflows.LocalActivityOptions { StartToCloseTimeout = System.TimeSpan.FromMinutes(1) });
                }
            }
            """);

    [Fact]
    public Task LocalActivity_BlockingIo_Reports()
        => Verify(Stubs + """
            public static class A
            {
                [Temporalio.Activities.Activity]
                public static async System.Threading.Tasks.Task Run()
                {
                    await {|TMP4107:System.Threading.Tasks.Task.Delay(100)|};
                }
            }

            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task RunAsync()
                {
                    await Temporalio.Workflows.Workflow.ExecuteLocalActivityAsync(
                        () => A.Run(),
                        new Temporalio.Workflows.LocalActivityOptions { StartToCloseTimeout = System.TimeSpan.FromMinutes(1) });
                }
            }
            """);

    [Fact]
    public Task LocalActivity_HttpClient_Reports()
        => Verify(Stubs + """
            public static class A
            {
                [Temporalio.Activities.Activity]
                public static async System.Threading.Tasks.Task Run()
                {
                    var client = new System.Net.Http.HttpClient();
                    await {|TMP4107:client.GetAsync("https://example.com")|};
                }
            }

            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task RunAsync()
                {
                    await Temporalio.Workflows.Workflow.ExecuteLocalActivityAsync(
                        () => A.Run(),
                        new Temporalio.Workflows.LocalActivityOptions { StartToCloseTimeout = System.TimeSpan.FromMinutes(1) });
                }
            }
            """);

    [Fact]
    public Task RegularActivity_BlockingIo_DoesNotReport()
        => Verify(Stubs + """
            public static class A
            {
                [Temporalio.Activities.Activity]
                public static async System.Threading.Tasks.Task Run()
                {
                    await System.Threading.Tasks.Task.Delay(100);
                }
            }

            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task RunAsync()
                {
                    await Temporalio.Workflows.Workflow.ExecuteActivityAsync(
                        () => A.Run(),
                        new Temporalio.Workflows.ActivityOptions { StartToCloseTimeout = System.TimeSpan.FromMinutes(1) });
                }
            }
            """);

    [Fact]
    public Task HeavyCpuLoop_EnabledViaEditorConfig_Reports()
        => VerifyWithConfig(
            Stubs + """
                [Temporalio.Workflows.Workflow]
                public class W
                {
                    [Temporalio.Workflows.WorkflowRun]
                    public void Run()
                    {
                        {|TMP4104:for|} (var i = 0; i < 10; i++)
                        {
                            var x = i * i;
                        }
                    }
                }
                """,
            "root = true\n\n[*.cs]\ndotnet_diagnostic.TMP4104.severity = warning\n");

    [Fact]
    public Task BusyPollingVersionFlag_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    {|TMP4108:while|} (true)
                    {
                        if (Temporalio.Workflows.Workflow.TargetWorkerDeploymentVersionChanged)
                        {
                            return;
                        }

                        await Temporalio.Workflows.Workflow.DelayAsync(100);
                    }
                }
            }
            """);

    [Fact]
    public Task VersionFlagCheckedAtBoundary_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    if (Temporalio.Workflows.Workflow.TargetWorkerDeploymentVersionChanged)
                    {
                        return;
                    }

                    await Temporalio.Workflows.Workflow.DelayAsync(100);
                }
            }
            """);

    [Fact]
    public Task LoopWithoutDelayOrFlag_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    while (true)
                    {
                        await Temporalio.Workflows.Workflow.DelayAsync(100);
                    }
                }
            }
            """);
}
