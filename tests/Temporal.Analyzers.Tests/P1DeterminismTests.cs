using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Analyzers.Analyzers;

namespace Kogoshvili.Temporal.Analyzers.Tests;

public class P1DeterminismTests
{
    private const string Stubs = "using Temporalio.Workflows;\n" + TestStubs.Attributes + TestStubs.Sdk;

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
    public Task CryptoRandom_Create_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var rng = {|TMP0122:System.Security.Cryptography.RandomNumberGenerator.Create()|};
                }
            }
            """);

    [Fact]
    public Task CryptoRandom_GetInt32_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var n = {|TMP0122:System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 100)|};
                }
            }
            """);

    [Fact]
    public Task CryptoRandom_InActivity_DoesNotReport()
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
                    var n = System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 100);
                }
            }
            """);

    [Fact]
    public Task Finalizer_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run() { }

                ~{|TMP0171:MyWorkflow|}() { }
            }
            """);

    [Fact]
    public Task Finalizer_InNonWorkflow_DoesNotReport()
        => Verify(Stubs + """
            public class PlainClass
            {
                ~PlainClass() { }
            }
            """);

    [Fact]
    public Task SystemTimer_Constructor_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP0172:new System.Threading.Timer(_ => { }, null, 0, 1000)|};
                }
            }
            """);

    [Fact]
    public Task PeriodicTimer_Constructor_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP0172:new System.Threading.PeriodicTimer(System.TimeSpan.FromSeconds(1))|};
                }
            }
            """);

    [Fact]
    public Task Timer_Change_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run(System.Threading.Timer timer)
                {
                    {|TMP0172:timer.Change(0, 1000)|};
                }
            }
            """);

    [Fact]
    public Task SystemTimersTimer_Start_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run(System.Timers.Timer timer)
                {
                    {|TMP0172:timer.Start()|};
                }
            }
            """);

    [Fact]
    public Task Timer_InActivity_DoesNotReport()
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
                    var t = new System.Threading.Timer(_ => { }, null, 0, 1000);
                }
            }
            """);

    [Fact]
    public Task WeakReference_Constructor_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP0174:new System.WeakReference(new object())|};
                }
            }
            """);

    [Fact]
    public Task ConditionalWeakTable_Constructor_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP0174:new System.Runtime.CompilerServices.ConditionalWeakTable<object, object>()|};
                }
            }
            """);

    [Fact]
    public Task WeakReference_Target_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run(System.WeakReference wr)
                {
                    var t = {|TMP0174:wr.Target|};
                }
            }
            """);

    [Fact]
    public Task WeakReference_InActivity_DoesNotReport()
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
                    var wr = new System.WeakReference(new object());
                }
            }
            """);

    [Fact]
    public Task StaticConstructor_WorkflowCommand_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                static {|TMP0177:MyWorkflow|}()
                {
                    _ = Workflow.DelayAsync(100);
                }

                [Temporalio.Workflows.WorkflowRun]
                public void Run() { }
            }
            """);

    [Fact]
    public Task StaticFieldInitializer_WorkflowCommand_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                private static readonly System.Threading.Tasks.Task {|TMP0177:_t = Workflow.DelayAsync(100)|};

                [Temporalio.Workflows.WorkflowRun]
                public void Run() { }
            }
            """);

    [Fact]
    public Task ModuleInitializer_WorkflowCommand_Reports()
        => Verify(Stubs + """
            public static class Init
            {
                [System.Runtime.CompilerServices.ModuleInitializer]
                internal static void {|TMP0177:Initialize|}()
                {
                    _ = Workflow.DelayAsync(100);
                }
            }
            """);

    [Fact]
    public Task StaticConstructor_NoWorkflowCommand_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                private static readonly int Value = 42;

                static MyWorkflow()
                {
                    Value = 0;
                }

                [Temporalio.Workflows.WorkflowRun]
                public void Run() { }
            }
            """);

    [Fact]
    public Task ControlFlow_Randomness_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    if ({|TMP0175:{|TMP0121:System.Guid.NewGuid()|} == System.Guid.Empty|}) { }
                }
            }
            """);

    [Fact]
    public Task ControlFlow_WallClock_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run(System.DateTime deadline)
                {
                    while ({|TMP0175:{|TMP0101:System.DateTime.UtcNow|} < deadline|}) { }
                }
            }
            """);

    [Fact]
    public Task ControlFlow_DeterministicSource_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    if (Workflow.Random.Next(0, 10) > 5) { }
                }
            }
            """);

    [Fact]
    public Task WallClockComparison_UtcNowVersusPersisted_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run(System.DateTime expiry)
                {
                    if ({|TMP0104:Workflow.UtcNow|} > expiry) { }
                }
            }
            """);

    [Fact]
    public Task WallClockComparison_NoUtcNow_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run(System.DateTime a, System.DateTime b)
                {
                    if (a > b) { }
                }
            }
            """);

    [Fact]
    public Task WallClockComparison_UtcNowArithmetic_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var expiry = Workflow.UtcNow.AddHours(1);
                    if (expiry > Workflow.UtcNow) { }
                }
            }
            """);

    [Fact]
    public Task NewGuid_AsActivityArgument_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    _ = Workflow.ExecuteActivityAsync("act", new object[] { {|TMP0123:Workflow.NewGuid()|} }, new ActivityOptions());
                }
            }
            """);

    [Fact]
    public Task WorkflowRandom_AsActivityArgument_InWorkflow_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    _ = Workflow.ExecuteActivityAsync("act", new object[] { {|TMP0123:Workflow.Random.Next()|} }, new ActivityOptions());
                }
            }
            """);

    [Fact]
    public Task NewGuid_NotPassedToCommand_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var id = Workflow.NewGuid();
                }
            }
            """);
}
