using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Analyzers.Analyzers;

namespace Kogoshvili.Temporal.Analyzers.Tests;

public class SdkMisuseP2Tests
{
    private const string Stubs = TestStubs.Attributes + TestStubs.Sdk;

    private static Task Verify(string source)
    {
        var test = new CSharpAnalyzerTest<SdkMisuseAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test.RunAsync();
    }

    [Fact]
    public Task BigIntegerParam_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run(System.Numerics.BigInteger {|TMP2142:x|})
                    => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task ExceptionParam_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run(System.Exception {|TMP2143:x|})
                    => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task NestedLossyMember_Reports()
        => Verify(Stubs + """
            public class Dto
            {
                public object {|TMP2172:Value|} { get; set; }
            }

            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run(Dto d) => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task LargeCollectionPayload_Reports()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var x = new int[] {|TMP2144:{ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21 }|};
                }
            }
            """);

    [Fact]
    public Task RetryPolicyOnNonIdempotentActivity_Reports()
        => Verify(Stubs + """
            public static class A
            {
                [Temporalio.Activities.Activity]
                public static System.Threading.Tasks.Task Process(string idempotencyKey)
                    => System.Threading.Tasks.Task.CompletedTask;
            }

            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    await {|TMP2106:Temporalio.Workflows.Workflow.ExecuteActivityAsync(
                        () => A.Process("k"),
                        new Temporalio.Workflows.ActivityOptions
                        {
                            StartToCloseTimeout = System.TimeSpan.FromMinutes(1),
                            RetryPolicy = new Temporalio.Workflows.RetryPolicy(),
                        })|};
                }
            }
            """);

    [Fact]
    public Task NonIdempotentActivityWithoutKey_Reports()
        => Verify(Stubs + """
            public class A
            {
                [Temporalio.Activities.Activity]
                public System.Threading.Tasks.Task {|TMP2107:Process|}()
                    => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    [Fact]
    public Task IdempotentActivityWithoutKey_DoesNotReport()
        => Verify(Stubs + """
            public class A
            {
                [Temporalio.Activities.Activity]
                public System.Threading.Tasks.Task GetData() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);
}
