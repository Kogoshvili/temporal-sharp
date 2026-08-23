using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Analyzers.Analyzers;
using Kogoshvili.Temporal.Analyzers.CodeFixes;

namespace Kogoshvili.Temporal.Analyzers.Tests;

public class WorkflowApiReplacementCodeFixTests
{
    private static Task VerifyReplacement<TAnalyzer, TCodeFix>(string source, string fixedSource)
        where TAnalyzer : Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer, new()
        where TCodeFix : Microsoft.CodeAnalysis.CodeFixes.CodeFixProvider, new()
    {
        var test = new CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            NumberOfIncrementalIterations = 1,
            NumberOfFixAllIterations = 0,
        };
        return test.RunAsync();
    }

    [Fact]
    public Task GuidNewGuid_ReplacedWithWorkflowNewGuid()
        => VerifyReplacement<DeterminismAnalyzer, WorkflowApiReplacementCodeFixProvider>(
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var g = {|TMP0121:System.Guid.NewGuid()|};
                }
            }
            """,
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var g = Temporalio.Workflows.Workflow.NewGuid();
                }
            }
            """);

    [Fact]
    public Task NewRandom_ReplacedWithWorkflowRandom()
        => VerifyReplacement<DeterminismAnalyzer, WorkflowApiReplacementCodeFixProvider>(
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var r = {|TMP0121:new System.Random()|};
                }
            }
            """,
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var r = Temporalio.Workflows.Workflow.Random;
                }
            }
            """);

    [Fact]
    public Task DateTimeNow_ReplacedWithWorkflowUtcNow()
        => VerifyReplacement<DeterminismAnalyzer, WorkflowApiReplacementCodeFixProvider>(
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var now = {|TMP0101:System.DateTime.Now|};
                }
            }
            """,
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var now = Temporalio.Workflows.Workflow.UtcNow;
                }
            }
            """);

    [Fact]
    public Task TaskDelay_ReplacedWithWorkflowDelayAsync()
        => VerifyReplacement<DeterminismAnalyzer, WorkflowApiReplacementCodeFixProvider>(
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    await {|TMP0111:System.Threading.Tasks.Task.Delay(100)|};
                }
            }
            """,
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    await Temporalio.Workflows.Workflow.DelayAsync(100);
                }
            }
            """);

    [Fact]
    public Task TaskWhenAll_ReplacedWithWorkflowWhenAllAsync()
        => VerifyReplacement<DeterminismAnalyzer, WorkflowApiReplacementCodeFixProvider>(
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var t1 = System.Threading.Tasks.Task.CompletedTask;
                    var t2 = System.Threading.Tasks.Task.CompletedTask;
                    await {|TMP0148:System.Threading.Tasks.Task.WhenAll(t1, t2)|};
                }
            }
            """,
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var t1 = System.Threading.Tasks.Task.CompletedTask;
                    var t2 = System.Threading.Tasks.Task.CompletedTask;
                    await Temporalio.Workflows.Workflow.WhenAllAsync(t1, t2);
                }
            }
            """);

    [Fact]
    public Task TaskRun_ReplacedWithWorkflowRunTaskAsync()
        => VerifyReplacement<DeterminismAnalyzer, WorkflowApiReplacementCodeFixProvider>(
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    await {|TMP0146:System.Threading.Tasks.Task.Run(() => DoAsync())|};
                }

                private static async System.Threading.Tasks.Task DoAsync() { }
            }
            """,
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    await Temporalio.Workflows.Workflow.RunTaskAsync(() => DoAsync());
                }

                private static async System.Threading.Tasks.Task DoAsync() { }
            }
            """);
}

public class LoggingCodeFixTests
{
    private static Task Verify(string source, string fixedSource)
    {
        var test = new CSharpCodeFixTest<SdkMisuseAnalyzer, LoggingCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            NumberOfIncrementalIterations = 1,
            NumberOfFixAllIterations = 0,
        };
        return test.RunAsync();
    }

    private static Task VerifyActivity(string source, string fixedSource)
    {
        var test = new CSharpCodeFixTest<ActivityContextAnalyzer, LoggingCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            NumberOfIncrementalIterations = 1,
            NumberOfFixAllIterations = 0,
        };
        return test.RunAsync();
    }

    [Fact]
    public Task ConsoleWriteLine_ReplacedWithWorkflowLogger()
        => Verify(
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    {|TMP2131:System.Console.WriteLine("x")|};
                }
            }
            """,
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    Temporalio.Workflows.Workflow.Logger.LogInformation("x");
                }
            }
            """);

    [Fact]
    public Task DebugWriteLine_ReplacedWithLogDebug()
        => Verify(
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    {|TMP2131:System.Diagnostics.Debug.WriteLine("x")|};
                }
            }
            """,
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    Temporalio.Workflows.Workflow.Logger.LogDebug("x");
                }
            }
            """);

    [Fact]
    public Task TraceTraceError_ReplacedWithLogError()
        => Verify(
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    {|TMP2131:System.Diagnostics.Trace.TraceError("x")|};
                }
            }
            """,
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    Temporalio.Workflows.Workflow.Logger.LogError("x");
                }
            }
            """);

    [Fact]
    public Task ActivityConsoleWriteLine_ReplacedWithActivityContextLog()
        => VerifyActivity(
            TestStubs.Attributes + TestStubs.Sdk + """
            public class A
            {
                [Temporalio.Activities.Activity]
                public async System.Threading.Tasks.Task Run()
                {
                    {|TMP3106:System.Console.WriteLine("x")|};
                }
            }
            """,
            TestStubs.Attributes + TestStubs.Sdk + """
            public class A
            {
                [Temporalio.Activities.Activity]
                public async System.Threading.Tasks.Task Run()
                {
                    Temporalio.Activities.ActivityExecutionContext.Current.Logger.LogInformation("x");
                }
            }
            """);
}

public class RemoveAssertCodeFixTests
{
    [Fact]
    public Task DebugAssert_Removed()
    {
        var test = new CSharpCodeFixTest<ErrorHandlingAnalyzer, RemoveAssertCodeFixProvider, DefaultVerifier>
        {
            TestCode = TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    {|TMP2133:System.Diagnostics.Debug.Assert(true)|};
                    return;
                }
            }
            """,
            FixedCode = TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    return;
                }
            }
            """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            NumberOfIncrementalIterations = 1,
            NumberOfFixAllIterations = 0,
        };
        return test.RunAsync();
    }
}

public class CodeFixTests
{
    private static Task VerifyBlocking(string source, string fixedSource)
    {
        var test = new CSharpCodeFixTest<DeterminismAnalyzer, BlockingTaskCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            NumberOfIncrementalIterations = 1,
            NumberOfFixAllIterations = 0,
        };
        return test.RunAsync();
    }

    private static Task VerifyFloating(string source, string fixedSource, string equivalenceKey)
    {
        var test = new CSharpCodeFixTest<DeterminismAnalyzer, FloatingTaskCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            CodeActionEquivalenceKey = equivalenceKey,
            NumberOfIncrementalIterations = 1,
            NumberOfFixAllIterations = 0,
        };
        return test.RunAsync();
    }

    [Fact]
    public Task BlockingResult_ReplacedWithAwait()
        => VerifyBlocking(
            TestStubs.Attributes + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var t = System.Threading.Tasks.Task.FromResult(1);
                    var v = {|TMP0111:t.Result|};
                }
            }
            """,
            TestStubs.Attributes + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var t = System.Threading.Tasks.Task.FromResult(1);
                    var v = await t;
                }
            }
            """);

    [Fact]
    public Task BlockingWait_ReplacedWithAwaitAndAsync()
        => VerifyBlocking(
            TestStubs.Attributes + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var t = System.Threading.Tasks.Task.CompletedTask;
                    {|TMP0111:t.Wait()|};
                }
            }
            """,
            TestStubs.Attributes + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async void Run()
                {
                    var t = System.Threading.Tasks.Task.CompletedTask;
                    await t;
                }
            }
            """);

    [Fact]
    public Task BlockingGetAwaiterGetResult_ReplacedWithAwait()
        => VerifyBlocking(
            TestStubs.Attributes + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var t = System.Threading.Tasks.Task.FromResult(1);
                    var v = {|TMP0111:t.GetAwaiter().GetResult()|};
                }
            }
            """,
            TestStubs.Attributes + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var t = System.Threading.Tasks.Task.FromResult(1);
                    var v = await t;
                }
            }
            """);

    [Fact]
    public Task FloatingTask_Discarded()
        => VerifyFloating(
            TestStubs.Attributes + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP0112:DoWorkAsync()|};
                }

                public static System.Threading.Tasks.Task DoWorkAsync() => System.Threading.Tasks.Task.CompletedTask;
            }
            """,
            TestStubs.Attributes + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    _ = DoWorkAsync();
                }

                public static System.Threading.Tasks.Task DoWorkAsync() => System.Threading.Tasks.Task.CompletedTask;
            }
            """,
            equivalenceKey: "discard");

    [Fact]
    public Task FloatingTask_Awaited()
        => VerifyFloating(
            TestStubs.Attributes + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP0112:DoWorkAsync()|};
                }

                public static System.Threading.Tasks.Task DoWorkAsync() => System.Threading.Tasks.Task.CompletedTask;
            }
            """,
            TestStubs.Attributes + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async void Run()
                {
                    await DoWorkAsync();
                }

                public static System.Threading.Tasks.Task DoWorkAsync() => System.Threading.Tasks.Task.CompletedTask;
            }
            """,
            equivalenceKey: "await");
}

public class BlockingSyncReplacementCodeFixTests
{
    private static Task Verify(string source, string fixedSource)
    {
        var test = new CSharpCodeFixTest<DeterminismAnalyzer, BlockingSyncReplacementCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            NumberOfIncrementalIterations = 1,
            NumberOfFixAllIterations = 0,
        };
        return test.RunAsync();
    }

    [Fact]
    public Task MutexWaitOne_ReplacedWithWaitOneAsync()
        => Verify(
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                private readonly System.Threading.Mutex _mutex = new System.Threading.Mutex();

                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    {|TMP0147:_mutex.WaitOne()|};
                }
            }
            """,
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                private readonly Temporalio.Workflows.Mutex _mutex = new Temporalio.Workflows.Mutex();

                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    await _mutex.WaitOneAsync();
                }
            }
            """);

    [Fact]
    public Task SemaphoreSlimWait_ReplacedWithSemaphoreWaitAsync()
        => Verify(
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var _sem = new System.Threading.SemaphoreSlim(1);
                    {|TMP0147:_sem.Wait()|};
                }
            }
            """,
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var _sem = new Temporalio.Workflows.Semaphore(1);
                    await _sem.WaitAsync();
                }
            }
            """);

    [Fact]
    public Task SemaphoreWaitOne_ReplacedWithWaitAsync()
        => Verify(
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var _sem = new System.Threading.Semaphore(1, 1);
                    {|TMP0147:_sem.WaitOne()|};
                }
            }
            """,
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var _sem = new Temporalio.Workflows.Semaphore(1);
                    await _sem.WaitAsync();
                }
            }
            """);

    [Fact]
    public Task SemaphoreSlimEqualCounts_ReplacedWithSingleArgSemaphore()
        => Verify(
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var _sem = new System.Threading.SemaphoreSlim(2, 2);
                    {|TMP0147:_sem.Wait()|};
                }
            }
            """,
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var _sem = new Temporalio.Workflows.Semaphore(2);
                    await _sem.WaitAsync();
                }
            }
            """);

    [Fact]
    public Task SemaphoreNamedArgs_ReplacedWithSingleArgSemaphore()
        => Verify(
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var _sem = new System.Threading.Semaphore(initialCount: 3, maximumCount: 3);
                    {|TMP0147:_sem.WaitOne()|};
                }
            }
            """,
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var _sem = new Temporalio.Workflows.Semaphore(3);
                    await _sem.WaitAsync();
                }
            }
            """);

    [Fact]
    public Task SemaphoreTargetTyped_ReplacedWithSingleArgSemaphore()
        => Verify(
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    System.Threading.Semaphore _sem = new(2, 2);
                    {|TMP0147:_sem.WaitOne()|};
                }
            }
            """,
            TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    Temporalio.Workflows.Semaphore _sem = new(2);
                    await _sem.WaitAsync();
                }
            }
            """);
}
