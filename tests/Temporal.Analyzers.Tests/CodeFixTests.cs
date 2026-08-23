using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Analyzers.Analyzers;
using Kogoshvili.Temporal.Analyzers.CodeFixes;

namespace Kogoshvili.Temporal.Analyzers.Tests;

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
