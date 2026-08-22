using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using TemporalSharp.Analyzers.Analyzers;

namespace TemporalSharp.Analyzers.Tests;

public class SearchAttributeAnalyzerTests
{
    private const string Stubs = TestStubs.Attributes + TestStubs.Sdk;

    private static CSharpAnalyzerTest<SearchAttributeAnalyzer, DefaultVerifier> CreateTest(string source)
    {
        var test = new CSharpAnalyzerTest<SearchAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", """
            root = true

            [*.cs]
            dotnet_diagnostic.TMP2161.severity = warning
            temporalsharp.search_attributes = user_id=user_id
            """));
        return test;
    }

    [Fact]
    public Task MappedFieldNeverUpserted_Reports()
        => CreateTest(Stubs + """
            public class MyInput
            {
                public string {|TMP2161:UserId|} { get; set; }
            }

            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run(MyInput input)
                    => System.Threading.Tasks.Task.CompletedTask;
            }
            """).RunAsync();

    [Fact]
    public Task MappedFieldUpserted_DoesNotReport()
        => CreateTest(Stubs + """
            public class MyInput
            {
                public string UserId { get; set; }
            }

            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run(MyInput input)
                {
                    Temporalio.Workflows.Workflow.UpsertTypedSearchAttributes(
                        Temporalio.Workflows.SearchAttributeKey.ForKeyword("user_id"));
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """).RunAsync();

    [Fact]
    public Task UnmappedField_DoesNotReport()
        => CreateTest(Stubs + """
            public class MyInput
            {
                public string DisplayName { get; set; }
            }

            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run(MyInput input)
                    => System.Threading.Tasks.Task.CompletedTask;
            }
            """).RunAsync();
}
