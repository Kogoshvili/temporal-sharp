using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Analyzers.Analyzers;

namespace Kogoshvili.Temporal.Analyzers.Tests;

public class ActivityContextAnalyzerTests
{
    private const string Stubs = TestStubs.Attributes + TestStubs.Sdk;

    private static Task Verify(string source)
    {
        var test = new CSharpAnalyzerTest<ActivityContextAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test.RunAsync();
    }

    [Fact]
    public Task ContextCapturedAcrossAwait_Reports()
        => Verify(Stubs + """
            public class A
            {
                [Temporalio.Activities.Activity]
                public async System.Threading.Tasks.Task Work()
                {
                    var ctx = {|TMP3105:Temporalio.Activities.ActivityExecutionContext.Current|};
                    await System.Threading.Tasks.Task.Delay(1);
                }
            }
            """);

    [Fact]
    public Task ContextUsedWithoutAwait_DoesNotReport()
        => Verify(Stubs + """
            public class A
            {
                [Temporalio.Activities.Activity]
                public System.Threading.Tasks.Task Work()
                {
                    var ctx = Temporalio.Activities.ActivityExecutionContext.Current;
                    ctx.Heartbeat();
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """);

    [Fact]
    public Task ConsoleLogInActivity_Reports()
        => Verify(Stubs + """
            public class A
            {
                [Temporalio.Activities.Activity]
                public System.Threading.Tasks.Task Work()
                {
                    {|TMP3106:System.Console.WriteLine("x")|};
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """);

    [Fact]
    public Task ConsoleLogOutsideActivity_DoesNotReport()
        => Verify(Stubs + """
            public static class Helper
            {
                public static void Log() { System.Console.WriteLine("x"); }
            }
            """);

    [Fact]
    public Task HttpClientWithoutCancellation_Reports()
        => Verify(Stubs + """
            public class A
            {
                [Temporalio.Activities.Activity]
                public async System.Threading.Tasks.Task Work()
                {
                    var client = new System.Net.Http.HttpClient();
                    await {|TMP3107:client.GetAsync("https://example.com")|};
                }
            }
            """);

    [Fact]
    public Task HttpClientWithCancellation_DoesNotReport()
        => Verify(Stubs + """
            public class A
            {
                [Temporalio.Activities.Activity]
                public async System.Threading.Tasks.Task Work()
                {
                    var client = new System.Net.Http.HttpClient();
                    await client.GetAsync("https://example.com", System.Threading.CancellationToken.None);
                }
            }
            """);
}
