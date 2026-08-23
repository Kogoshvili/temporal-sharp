using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Analyzers.Analyzers;

namespace Kogoshvili.Temporal.Analyzers.Tests;

public class ActivityHeartbeatAnalyzerTests
{
    private const string Stubs = TestStubs.Attributes + TestStubs.Sdk;

    private static Task Verify(string source)
    {
        var test = new CSharpAnalyzerTest<ActivityHeartbeatAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test.RunAsync();
    }

    [Fact]
    public Task LoopWithoutHeartbeat_Reports()
        => Verify(Stubs + """
            public static class Act
            {
                [Temporalio.Activities.Activity]
                public static System.Threading.Tasks.Task {|TMP3101:Do|}()
                {
                    for (int i = 0; i < 3; i++) { }
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """);

    [Fact]
    public Task LoopWithHeartbeat_DoesNotReport()
        => Verify(Stubs + """
            public static class Act
            {
                [Temporalio.Activities.Activity]
                public static System.Threading.Tasks.Task Do()
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Temporalio.Activities.ActivityExecutionContext.Current.Heartbeat();
                    }
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """);

    [Fact]
    public Task HeartbeatTimeoutWithoutHeartbeat_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    await {|TMP3102:Temporalio.Workflows.Workflow.ExecuteActivityAsync(() => Act.Do(), new Temporalio.Workflows.ActivityOptions { HeartbeatTimeout = System.TimeSpan.FromMinutes(1) })|};
                }
            }

            public static class Act
            {
                [Temporalio.Activities.Activity]
                public static System.Threading.Tasks.Task Do() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task HeartbeatTimeoutViaVariable_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var opts = new Temporalio.Workflows.ActivityOptions { HeartbeatTimeout = System.TimeSpan.FromMinutes(1) };
                    await {|TMP3102:Temporalio.Workflows.Workflow.ExecuteActivityAsync(() => Act.Do(), opts)|};
                }
            }

            public static class Act
            {
                [Temporalio.Activities.Activity]
                public static System.Threading.Tasks.Task Do() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task HeartbeatTimeoutWithHeartbeat_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    await Temporalio.Workflows.Workflow.ExecuteActivityAsync(
                        () => Act.Do(),
                        new Temporalio.Workflows.ActivityOptions { HeartbeatTimeout = System.TimeSpan.FromMinutes(1) });
                }
            }

            public static class Act
            {
                [Temporalio.Activities.Activity]
                public static System.Threading.Tasks.Task Do()
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Temporalio.Activities.ActivityExecutionContext.Current.Heartbeat();
                    }
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """);

    [Fact]
    public Task HeartbeatWithoutTimeout_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    await {|TMP3103:Temporalio.Workflows.Workflow.ExecuteActivityAsync(() => Act.Do(), new Temporalio.Workflows.ActivityOptions { StartToCloseTimeout = System.TimeSpan.FromMinutes(1) })|};
                }
            }

            public static class Act
            {
                [Temporalio.Activities.Activity]
                public static System.Threading.Tasks.Task Do()
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Temporalio.Activities.ActivityExecutionContext.Current.Heartbeat();
                    }
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """);

    [Fact]
    public Task UnnecessaryHeartbeat_Reports()
        => Verify(Stubs + """
            public static class Act
            {
                [Temporalio.Activities.Activity]
                public static System.Threading.Tasks.Task {|TMP3104:Do|}()
                {
                    Temporalio.Activities.ActivityExecutionContext.Current.Heartbeat();
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """);

    [Fact]
    public Task SingleAwaitWithHeartbeat_ReportsUnnecessary()
        => Verify(Stubs + """
            public static class Act
            {
                [Temporalio.Activities.Activity]
                public static async System.Threading.Tasks.Task {|TMP3104:Do|}()
                {
                    Temporalio.Activities.ActivityExecutionContext.Current.Heartbeat();
                    await System.Threading.Tasks.Task.Delay(1);
                }
            }
            """);
}
