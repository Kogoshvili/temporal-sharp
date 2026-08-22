using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using TemporalSharp.Analyzers.Analyzers;

namespace TemporalSharp.Analyzers.Tests;

public class SdkMisuseAnalyzerTests
{
    private static Task Verify(string source)
    {
        var test = new CSharpAnalyzerTest<SdkMisuseAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test.RunAsync();
    }

    private static string Stubs => TestStubs.Attributes + TestStubs.Sdk;

    [Fact]
    public Task ActivityOptionsMissingTimeout_Reports()
        => Verify(Stubs + """
            public class C
            {
                public void M()
                {
                    var opts = {|TMP2101:new Temporalio.Workflows.ActivityOptions { TaskQueue = "x" }|};
                }
            }
            """);

    [Fact]
    public Task ActivityOptionsWithStartToCloseTimeout_DoesNotReport()
        => Verify(Stubs + """
            public class C
            {
                public void M()
                {
                    var opts = new Temporalio.Workflows.ActivityOptions
                    {
                        StartToCloseTimeout = System.TimeSpan.FromSeconds(1),
                    };
                }
            }
            """);

    [Fact]
    public Task ActivityOptionsEmptyInitializer_DoesNotReport()
        => Verify(Stubs + """
            public class C
            {
                public void M()
                {
                    var opts = new Temporalio.Workflows.ActivityOptions();
                }
            }
            """);

    [Fact]
    public Task StringTarget_Reports()
        => Verify(Stubs + """
            public class C
            {
                public void M()
                {
                    var opts = new Temporalio.Workflows.ActivityOptions();
                    var t = {|TMP2111:Temporalio.Workflows.Workflow.ExecuteActivityAsync("Greet", null, opts)|};
                }
            }
            """);

    [Fact]
    public Task TypedLambdaTarget_DoesNotReport()
        => Verify(Stubs + """
            public class C
            {
                public void M()
                {
                    var opts = new Temporalio.Workflows.ActivityOptions();
                    var t = Temporalio.Workflows.Workflow.ExecuteActivityAsync(() => null, opts);
                }
            }
            """);

    [Fact]
    public Task ContinueAsNewDiscarded_Reports()
        => Verify(Stubs + """
            public class C
            {
                public void M()
                {
                    var opts = new Temporalio.Workflows.ContinueAsNewOptions();
                    {|TMP2121:Temporalio.Workflows.Workflow.CreateContinueAsNewException(() => null, opts)|};
                }
            }
            """);

    [Fact]
    public Task ContinueAsNewThrown_DoesNotReport()
        => Verify(Stubs + """
            public class C
            {
                public void M()
                {
                    var opts = new Temporalio.Workflows.ContinueAsNewOptions();
                    throw Temporalio.Workflows.Workflow.CreateContinueAsNewException(() => null, opts);
                }
            }
            """);

    [Fact]
    public Task ConsoleLogging_InWorkflow_Reports()
        => Verify(TestStubs.Attributes + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP2131:System.Console.WriteLine("x")|};
                }
            }
            """);

    [Fact]
    public Task ConsoleLogging_InNonWorkflow_DoesNotReport()
        => Verify(Stubs + """
            public class PlainClass
            {
                public void M()
                {
                    System.Console.WriteLine("x");
                }
            }
            """);

    [Fact]
    public Task NonSerializableStreamParam_Reports()
        => Verify(Stubs + """
            public static class Act
            {
                [Temporalio.Activities.Activity]
                public static System.Threading.Tasks.Task<int> Do(System.IO.Stream {|TMP2141:s|})
                    => System.Threading.Tasks.Task.FromResult(0);
            }
            """);

    [Fact]
    public Task NonSerializableDelegateParam_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run(System.Func<int> {|TMP2141:f|})
                    => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task ScheduleToCloseWithoutStart_Reports_WhenOptedIn()
    {
        var test = new CSharpAnalyzerTest<SdkMisuseAnalyzer, DefaultVerifier>
        {
            TestCode = Stubs + """
                public class C
                {
                    public void M()
                    {
                        var opts = {|TMP2102:new Temporalio.Workflows.ActivityOptions { ScheduleToCloseTimeout = System.TimeSpan.FromSeconds(1) }|};
                    }
                }
                """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", """
            root = true
            dotnet_diagnostic.TMP2102.severity = warning
            """));
        return test.RunAsync();
    }

    [Fact]
    public Task SensitiveParameter_Reports_WhenOptedIn()
    {
        var test = new CSharpAnalyzerTest<SdkMisuseAnalyzer, DefaultVerifier>
        {
            TestCode = Stubs + """
                [Temporalio.Workflows.Workflow]
                public class W
                {
                    [Temporalio.Workflows.WorkflowRun]
                    public System.Threading.Tasks.Task Run(string {|TMP2151:password|})
                        => System.Threading.Tasks.Task.CompletedTask;
                }
                """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", """
            root = true
            dotnet_diagnostic.TMP2151.severity = warning
            """));
        return test.RunAsync();
    }
}
