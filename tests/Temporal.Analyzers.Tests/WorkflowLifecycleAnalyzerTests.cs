using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Analyzers.Analyzers;

namespace Kogoshvili.Temporal.Analyzers.Tests;

public class WorkflowLifecycleAnalyzerTests
{
    private const string Stubs = TestStubs.Attributes + TestStubs.Sdk;

    private static Task Verify(string source)
    {
        var test = new CSharpAnalyzerTest<WorkflowLifecycleAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test.RunAsync();
    }

    [Fact]
    public Task ContinueAsNewWithNullState_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    {|TMP2122:Temporalio.Workflows.Workflow.CreateContinueAsNewException(
                        "wf", null, new Temporalio.Workflows.ContinueAsNewOptions())|};
                }
            }
            """);

    [Fact]
    public Task ContinueAsNewWithState_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    Temporalio.Workflows.Workflow.CreateContinueAsNewException(
                        "wf", new object[] { 1 }, new Temporalio.Workflows.ContinueAsNewOptions());
                }
            }
            """);

    [Fact]
    public Task ContinueAsNewWithStateAndEmptyOptions_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run()
                {
                    Temporalio.Workflows.Workflow.CreateContinueAsNewException(
                        "wf", new object[] { 1 }, new Temporalio.Workflows.ContinueAsNewOptions { });
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """);

    [Fact]
    public Task ContinueAsNewLambdaWithState_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run()
                {
                    Temporalio.Workflows.Workflow.CreateContinueAsNewException(
                        () => Next(1), null);
                    return System.Threading.Tasks.Task.CompletedTask;
                }

                private System.Threading.Tasks.Task Next(int x) => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task ContinueAsNewLambdaWithoutState_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run()
                {
                    {|TMP2122:Temporalio.Workflows.Workflow.CreateContinueAsNewException(
                        () => Next(), null)|};
                    return System.Threading.Tasks.Task.CompletedTask;
                }

                private System.Threading.Tasks.Task Next() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task SwallowedCancellation_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    try { await Temporalio.Workflows.Workflow.DelayAsync(1); }
                    {|TMP2123:catch|} (System.OperationCanceledException) { }
                }
            }
            """);

    [Fact]
    public Task RethrownCancellation_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    try { await System.Threading.Tasks.Task.Delay(1); }
                    catch (System.Exception) { throw; }
                }
            }
            """);

    [Fact]
    public Task CancellationTokenChecked_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    try { await System.Threading.Tasks.Task.Delay(1); }
                    catch (System.Exception)
                    {
                        if (Temporalio.Workflows.Workflow.CancellationToken.IsCancellationRequested) { return; }
                    }
                }
            }
            """);

    [Fact]
    public Task ThrownCatchVariable_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    try { await System.Threading.Tasks.Task.Delay(1); }
                    catch (System.Exception ex) { throw ex; }
                }
            }
            """);

    [Fact]
    public Task WrappedIntoApplicationFailure_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    try { await System.Threading.Tasks.Task.Delay(1); }
                    catch (System.Exception ex)
                    {
                        throw new Temporalio.Exceptions.ApplicationFailureException("failed", ex);
                    }
                }
            }
            """);

    [Fact]
    public Task ThrownUnrelatedException_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    try { await Temporalio.Workflows.Workflow.DelayAsync(1); }
                    {|TMP2123:catch|} (System.Exception) { throw new System.Exception("else"); }
                }
            }
            """);

    [Fact]
    public Task BroadCatchWithoutCancellableWork_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    try { throw new System.Exception("boom"); }
                    catch (System.Exception) { return; }
                }
            }
            """);

    [Fact]
    public Task CancellableCleanup_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    try { await System.Threading.Tasks.Task.Delay(1); }
                    {|TMP2124:finally|} { await System.Threading.Tasks.Task.Delay(1); }
                }
            }
            """);

    [Fact]
    public Task NonCancellableCleanup_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    try { await System.Threading.Tasks.Task.Delay(1); }
                    finally
                    {
                        await System.Threading.Tasks.Task.Delay(1, System.Threading.CancellationToken.None);
                    }
                }
            }
            """);

    [Fact]
    public Task UnboundedLoop_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    {|TMP2125:while|} (true) { await System.Threading.Tasks.Task.Delay(1); }
                }
            }
            """);

    [Fact]
    public Task LoopWithContinueAsNewCheck_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    while (true)
                    {
                        if (Temporalio.Workflows.Workflow.ContinueAsNewSuggested) { break; }
                        await System.Threading.Tasks.Task.Delay(1);
                    }
                }
            }
            """);

    [Fact]
    public Task CancellationFilteredByWhen_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    try { await System.Threading.Tasks.Task.Delay(1); }
                    catch (System.Exception ex) when (Temporalio.Exceptions.TemporalException.IsCanceledException(ex)) { }
                }
            }
            """);

    [Fact]
    public Task LoopWithBreakExit_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var attempts = 0;
                    while (true)
                    {
                        attempts++;
                        if (attempts > 3) { break; }
                        await System.Threading.Tasks.Task.Delay(1);
                    }
                }
            }
            """);

    [Fact]
    public Task SignalWaitLoop_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    while (true)
                    {
                        await Temporalio.Workflows.Workflow.WaitConditionAsync(() => false);
                    }
                }
            }
            """);
}
