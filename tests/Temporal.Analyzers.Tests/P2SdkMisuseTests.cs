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
    {
        var elements = string.Join(", ", Enumerable.Range(1, 1001));
        var source = Stubs +
            "[Temporalio.Workflows.Workflow]\n" +
            "public class W\n" +
            "{\n" +
            "    [Temporalio.Workflows.WorkflowRun]\n" +
            "    public async System.Threading.Tasks.Task Run()\n" +
            "    {\n" +
            "        var x = new int[] {|TMP2144:{ " + elements + " }|};\n" +
            "    }\n" +
            "}\n";
        return Verify(source);
    }

    [Fact]
    public Task SmallCollectionPayload_DoesNotReport()
        => Verify(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task Run()
                {
                    var x = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21 };
                }
            }
            """);
}
