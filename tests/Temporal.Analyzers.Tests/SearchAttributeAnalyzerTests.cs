using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Analyzers.Analyzers;

namespace Kogoshvili.Temporal.Analyzers.Tests;

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
            kogoshvili.temporal.search_attributes = user_id=user_id
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
                        Temporalio.Workflows.SearchAttributeKey.ForKeyword("user_id").ValueSet("user_id"));
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

    [Fact]
    public Task UpsertInOneWorkflow_DoesNotSuppressAnother()
        => CreateTest(Stubs + """
            public class Input1
            {
                public string UserId { get; set; }
            }

            public class Input2
            {
                public string {|TMP2161:UserId|} { get; set; }
            }

            [Temporalio.Workflows.Workflow]
            public class W1
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run(Input1 input)
                {
                    Temporalio.Workflows.Workflow.UpsertTypedSearchAttributes(
                        Temporalio.Workflows.SearchAttributeKey.ForKeyword("user_id").ValueSet("user_id"));
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            }

            [Temporalio.Workflows.Workflow]
            public class W2
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run(Input2 input)
                    => System.Threading.Tasks.Task.CompletedTask;
            }
            """).RunAsync();

    [Fact]
    public Task UpsertValueMatchingAnotherAttribute_DoesNotSuppress()
    {
        var test = new CSharpAnalyzerTest<SearchAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = Stubs + """
                public class MyInput
                {
                    public string UserId { get; set; }
                    public string {|TMP2161:Status|} { get; set; }
                }

                [Temporalio.Workflows.Workflow]
                public class W
                {
                    [Temporalio.Workflows.WorkflowRun]
                    public System.Threading.Tasks.Task Run(MyInput input)
                    {
                        Temporalio.Workflows.Workflow.UpsertTypedSearchAttributes(
                            Temporalio.Workflows.SearchAttributeKey.ForKeyword("user_id").ValueSet("status"));
                        return System.Threading.Tasks.Task.CompletedTask;
                    }
                }
                """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", """
            root = true

            [*.cs]
            dotnet_diagnostic.TMP2161.severity = warning
            kogoshvili.temporal.search_attributes = user_id=user_id, status=status
            """));
        return test.RunAsync();
    }
}
