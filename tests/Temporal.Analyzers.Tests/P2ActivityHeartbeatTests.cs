using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Analyzers.Analyzers;

namespace Kogoshvili.Temporal.Analyzers.Tests;

public class ActivityHeartbeatP2Tests
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
    public Task HeartbeatTimeoutMuchShorterThanStartToClose_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var opts = new Temporalio.Workflows.ActivityOptions
                    {
                        StartToCloseTimeout = System.TimeSpan.FromMinutes(10),
                        HeartbeatTimeout = {|TMP3108:System.TimeSpan.FromSeconds(1)|},
                    };
                }
            }
            """);

    [Fact]
    public Task HeartbeatTimeoutReasonable_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var opts = new Temporalio.Workflows.ActivityOptions
                    {
                        StartToCloseTimeout = System.TimeSpan.FromSeconds(10),
                        HeartbeatTimeout = System.TimeSpan.FromSeconds(5),
                    };
                }
            }
            """);

    [Fact]
    public Task HeartbeatTimeoutSkillGoodExample_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var opts = new Temporalio.Workflows.ActivityOptions
                    {
                        StartToCloseTimeout = System.TimeSpan.FromMinutes(30),
                        HeartbeatTimeout = System.TimeSpan.FromMinutes(2),
                    };
                }
            }
            """);

    [Fact]
    public Task HeartbeatsInLoopWithoutCancellationCheck_Reports()
        => Verify(Stubs + """
            public class A
            {
                [Temporalio.Activities.Activity]
                public async System.Threading.Tasks.Task {|TMP3109:LongPoll|}()
                {
                    for (var i = 0; i < 10; i++)
                    {
                        Temporalio.Activities.ActivityExecutionContext.Current.Heartbeat();
                        await System.Threading.Tasks.Task.Delay(1);
                    }
                }
            }
            """);

    [Fact]
    public Task HeartbeatsInLoopWithCancellationCheck_DoesNotReport()
        => Verify(Stubs + """
            public class A
            {
                [Temporalio.Activities.Activity]
                public async System.Threading.Tasks.Task LongPoll(System.Threading.CancellationToken token)
                {
                    for (var i = 0; i < 10; i++)
                    {
                        Temporalio.Activities.ActivityExecutionContext.Current.Heartbeat();
                        if (token.IsCancellationRequested) { break; }
                        await System.Threading.Tasks.Task.Delay(1);
                    }
                }
            }
            """);

    [Fact]
    public Task HeartbeatsInLoopWithThrowIfCancellationRequested_DoesNotReport()
        => Verify(Stubs + """
            public class A
            {
                [Temporalio.Activities.Activity]
                public async System.Threading.Tasks.Task LongPoll(System.Threading.CancellationToken token)
                {
                    for (var i = 0; i < 10; i++)
                    {
                        Temporalio.Activities.ActivityExecutionContext.Current.Heartbeat();
                        token.ThrowIfCancellationRequested();
                        await System.Threading.Tasks.Task.Delay(1);
                    }
                }
            }
            """);

    [Fact]
    public Task HeartbeatsInLoopWithUnusedCancellationToken_Reports()
        => Verify(Stubs + """
            public class A
            {
                [Temporalio.Activities.Activity]
                public async System.Threading.Tasks.Task {|TMP3109:LongPoll|}(System.Threading.CancellationToken token)
                {
                    for (var i = 0; i < 10; i++)
                    {
                        Temporalio.Activities.ActivityExecutionContext.Current.Heartbeat();
                        await System.Threading.Tasks.Task.Delay(1);
                    }
                }
            }
            """);
}
