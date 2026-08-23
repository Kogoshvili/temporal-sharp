using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Analyzers.Analyzers;

namespace Kogoshvili.Temporal.Analyzers.Tests;

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
    public Task TaskResult_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var t = System.Threading.Tasks.Task.FromResult(1);
                    var v = {|TMP0111:t.Result|};
                }
            }
            """);

    [Fact]
    public Task TaskGetAwaiterGetResult_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var t = System.Threading.Tasks.Task.FromResult(1);
                    {|TMP0111:t.GetAwaiter().GetResult()|};
                }
            }
            """);

    [Fact]
    public Task ValueTaskResult_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var vt = new System.Threading.Tasks.ValueTask<int>(1);
                    var v = {|TMP0111:vt.Result|};
                }
            }
            """);

    [Fact]
    public Task ValueTaskGetAwaiterGetResult_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var vt = new System.Threading.Tasks.ValueTask<int>(1);
                    {|TMP0111:vt.GetAwaiter().GetResult()|};
                }
            }
            """);

    [Fact]
    public Task ConfigureAwaitFalse_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var t = System.Threading.Tasks.Task.FromResult(1);
                    {|TMP0113:t.ConfigureAwait(false)|};
                }
            }
            """);

    [Fact]
    public Task ConfigureAwaitTrue_InWorkflow_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var t = System.Threading.Tasks.Task.FromResult(1);
                    _ = t.ConfigureAwait(true);
                }
            }
            """);

    [Fact]
    public Task AwaitedTaskResult_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
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
                    {|TMP0146:System.Threading.Tasks.Task.Run(() => { })|};
                }
            }
            """);

    [Fact]
    public Task TaskFactoryStartNew_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP0146:System.Threading.Tasks.Task.Factory.StartNew(() => { })|};
                }
            }
            """);

    [Fact]
    public Task ThreadPoolQueueUserWorkItem_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP0141:System.Threading.ThreadPool.QueueUserWorkItem(_ => { })|};
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
    public Task TaskWhenAll_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var t = System.Threading.Tasks.Task.CompletedTask;
                    {|TMP0143:System.Threading.Tasks.Task.WhenAll(t)|};
                }
            }
            """);

    [Fact]
    public Task TaskWhenAny_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var t = System.Threading.Tasks.Task.CompletedTask;
                    {|TMP0143:System.Threading.Tasks.Task.WhenAny(t)|};
                }
            }
            """);

    [Fact]
    public Task TaskContinueWith_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var t = System.Threading.Tasks.Task.CompletedTask;
                    {|TMP0143:t.ContinueWith(x => { })|};
                }
            }
            """);

    [Fact]
    public Task TaskOfTContinueWith_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var t = System.Threading.Tasks.Task.FromResult("x");
                    {|TMP0143:t.ContinueWith(x => "done")|};
                }
            }
            """);

    [Fact]
    public Task CancellationTokenSourceCancelAsync_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var cts = new System.Threading.CancellationTokenSource();
                    {|TMP0143:cts.CancelAsync()|};
                }
            }
            """);

    [Fact]
    public Task TaskWhenAll_InActivity_DoesNotReport()
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
                    var t = System.Threading.Tasks.Task.CompletedTask;
                    System.Threading.Tasks.Task.WhenAll(t);
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
                    {|TMP0147:sem.Wait()|};
                }
            }
            """);

    [Fact]
    public Task SemaphoreWaitOne_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var sem = new System.Threading.Semaphore(1, 1);
                    {|TMP0147:sem.WaitOne()|};
                }
            }
            """);

    [Fact]
    public Task MutexWaitOne_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var mutex = new System.Threading.Mutex();
                    {|TMP0147:mutex.WaitOne()|};
                }
            }
            """);

    [Fact]
    public Task AutoResetEventWaitOne_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var evt = new System.Threading.AutoResetEvent(false);
                    {|TMP0142:evt.WaitOne()|};
                }
            }
            """);

    [Fact]
    public Task ManualResetEventWaitOne_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var evt = new System.Threading.ManualResetEvent(false);
                    {|TMP0142:evt.WaitOne()|};
                }
            }
            """);

    [Fact]
    public Task WaitHandleWaitAny_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var evt = new System.Threading.ManualResetEvent(false);
                    {|TMP0142:System.Threading.WaitHandle.WaitAny(new System.Threading.WaitHandle[] { evt })|};
                }
            }
            """);

    [Fact]
    public Task ReaderWriterLockAcquireWriterLock_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var rwl = new System.Threading.ReaderWriterLock();
                    {|TMP0142:rwl.AcquireWriterLock(-1)|};
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
    public Task TaskCompletionSource_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP0144:new System.Threading.Tasks.TaskCompletionSource<int>()|};
                }
            }
            """);

    [Fact]
    public Task TaskCompletionSource_InActivity_DoesNotReport()
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
                    var tcs = new System.Threading.Tasks.TaskCompletionSource<int>();
                }
            }
            """);

    [Fact]
    public Task ActivatorCreateInstance_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP0145:System.Activator.CreateInstance(typeof(int))|};
                }
            }
            """);

    [Fact]
    public Task MethodInfoInvoke_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var mi = typeof(object).GetMethod("ToString");
                    {|TMP0145:mi.Invoke(null, null)|};
                }
            }
            """);

    [Fact]
    public Task AsyncLocalCreation_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP1106:new System.Threading.AsyncLocal<int>()|};
                }
            }
            """);

    [Fact]
    public Task AsyncLocalValueAccess_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                private readonly System.Threading.AsyncLocal<int> _state = new System.Threading.AsyncLocal<int>();

                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var v = {|TMP1106:_state.Value|};
                }
            }
            """);

    [Fact]
    public Task ThreadLocalCreation_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP1106:new System.Threading.ThreadLocal<int>()|};
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
    public Task ForeachDictionaryKeys_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var d = new System.Collections.Generic.Dictionary<int, int>();
                    {|TMP0151:foreach|} (var k in d.Keys) { }
                }
            }
            """);

    [Fact]
    public Task DictionaryToList_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var d = new System.Collections.Generic.Dictionary<int, int>();
                    var l = {|TMP0151:System.Linq.Enumerable.ToList(d)|};
                }
            }
            """);

    [Fact]
    public Task DictionaryFirst_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var d = new System.Collections.Generic.Dictionary<int, int>();
                    var kv = {|TMP0151:System.Linq.Enumerable.First(d)|};
                }
            }
            """);

    [Fact]
    public Task DictionaryKeysToList_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var d = new System.Collections.Generic.Dictionary<int, int>();
                    var keys = {|TMP0151:System.Linq.Enumerable.ToList(d.Keys)|};
                }
            }
            """);

    [Fact]
    public Task DictionarySelectToList_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var d = new System.Collections.Generic.Dictionary<int, int>();
                    var keys = {|TMP0151:System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(d, kv => kv.Key))|};
                }
            }
            """);

    [Fact]
    public Task DictionaryOrderByToList_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var d = new System.Collections.Generic.Dictionary<int, int>();
                    var l = System.Linq.Enumerable.ToList(System.Linq.Enumerable.OrderBy(d, kv => kv.Key));
                }
            }
            """);

    [Fact]
    public Task ListFirst_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var list = new System.Collections.Generic.List<int>();
                    var x = System.Linq.Enumerable.First(list);
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

    [Fact]
    public Task StopwatchStartNew_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var sw = {|TMP0102:System.Diagnostics.Stopwatch.StartNew()|};
                }
            }
            """);

    [Fact]
    public Task StopwatchElapsed_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var sw = new System.Diagnostics.Stopwatch();
                    var elapsed = {|TMP0102:sw.Elapsed|};
                }
            }
            """);

    [Fact]
    public Task VirtualDispatchToOverride_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    Animal a = new Dog();
                    a.Make();
                }
            }

            public abstract class Animal
            {
                public abstract void Make();
            }

            public class Dog : Animal
            {
                public override void Make()
                {
                    var g = {|TMP0121:System.Guid.NewGuid()|};
                }
            }
            """);

    [Fact]
    public Task DelegateTargetCall_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    System.Action action = Helper.DoWork;
                    action();
                }
            }

            public static class Helper
            {
                public static void DoWork()
                {
                    var g = {|TMP0121:System.Guid.NewGuid()|};
                }
            }
            """);

    [Fact]
    public Task LongParse_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var x = {|TMP0161:long.Parse("42")|};
                }
            }
            """);

    [Fact]
    public Task DateTimeOffsetParse_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var x = {|TMP0161:System.DateTimeOffset.Parse("2026-01-01")|};
                }
            }
            """);

    [Fact]
    public Task IntTryParse_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    int n;
                    {|TMP0161:int.TryParse("42", out n)|};
                }
            }
            """);

    [Fact]
    public Task IntParseWithInvariantCulture_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var x = int.Parse("42", System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            """);

    [Fact]
    public Task IntToStringWithFormat_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    int n = 42;
                    var s = {|TMP0161:n.ToString("N0")|};
                }
            }
            """);

    [Fact]
    public Task IntToStringNoFormat_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    int n = 42;
                    var s = n.ToString();
                }
            }
            """);

    [Fact]
    public Task DoubleToString_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    double d = 3.14;
                    var s = {|TMP0161:d.ToString()|};
                }
            }
            """);

    [Fact]
    public Task DateToStringWithFormat_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var d = new System.DateTime(2026, 1, 1);
                    var s = {|TMP0161:d.ToString("yyyy-MM-dd")|};
                }
            }
            """);

    [Fact]
    public Task DateToStringWithProvider_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var d = new System.DateTime(2026, 1, 1);
                    var s = d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            """);

    [Fact]
    public Task StringFormat_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var s = {|TMP0161:string.Format("x={0}", 1)|};
                }
            }
            """);

    [Fact]
    public Task StringFormatWithProvider_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var s = string.Format(System.Globalization.CultureInfo.InvariantCulture, "x={0}", 1);
                }
            }
            """);

    [Fact]
    public Task GuidParse_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var g = System.Guid.Parse("00000000-0000-0000-0000-000000000000");
                }
            }
            """);

    [Fact]
    public Task BoolParse_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var b = bool.Parse("true");
                }
            }
            """);

    [Fact]
    public Task CultureSensitiveParse_InActivity_DoesNotReport()
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
                    var x = long.Parse("42");
                }
            }
            """);

    [Fact]
    public Task FloatingTask_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP0112:DoWorkAsync()|};
                }

                public static System.Threading.Tasks.Task DoWorkAsync() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task FloatingValueTask_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP0112:DoWorkAsync()|};
                }

                public static System.Threading.Tasks.ValueTask DoWorkAsync() => default;
            }
            """);

    [Fact]
    public Task FloatingTaskOfT_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP0112:GetValueAsync()|};
                }

                public static System.Threading.Tasks.Task<int> GetValueAsync() => System.Threading.Tasks.Task.FromResult(1);
            }
            """);

    [Fact]
    public Task FloatingTaskDiscarded_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    _ = DoWorkAsync();
                }

                public static System.Threading.Tasks.Task DoWorkAsync() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task FloatingTaskAssignedToVariable_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var task = DoWorkAsync();
                }

                public static System.Threading.Tasks.Task DoWorkAsync() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task FloatingTaskAwaited_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    await DoWorkAsync();
                }

                public static System.Threading.Tasks.Task DoWorkAsync() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task FloatingVoidCall_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    DoNothing();
                }

                public static void DoNothing() { }
            }
            """);

    [Fact]
    public Task FloatingTaskInActivity_DoesNotReport()
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
                    Helper.Go();
                }
            }

            public static class Helper
            {
                public static System.Threading.Tasks.Task Go() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);
}
