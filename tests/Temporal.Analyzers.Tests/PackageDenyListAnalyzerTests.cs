using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Analyzers.Analyzers;

namespace Kogoshvili.Temporal.Analyzers.Tests;

public class PackageDenyListAnalyzerTests
{
    private const string Config = """
        root = true

        [*.cs]
        dotnet_diagnostic.TMP2147.severity = warning
        kogoshvili.temporal.unsafe_namespaces = System.IO, System.Net.Http
        """;

    private static CSharpAnalyzerTest<PackageDenyListAnalyzer, DefaultVerifier> Create(string source, string editorConfig)
    {
        var test = new CSharpAnalyzerTest<PackageDenyListAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.Sources.Add(("/0/Stubs.cs", TestStubs.Attributes + TestStubs.Sdk));
        test.TestState.Sources.Add(("/0/Workflow.cs", source));
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", editorConfig));
        return test;
    }

    [Fact]
    public Task UnsafeUsingInWorkflowFile_Reports()
        => Create("""
            using {|TMP2147:System.IO|};

            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run() => System.Threading.Tasks.Task.CompletedTask;
            }
            """, Config).RunAsync();

    [Fact]
    public Task UnsafeSubNamespace_Reports()
        => Create("""
            using {|TMP2147:System.Net.Http.Headers|};

            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run() => System.Threading.Tasks.Task.CompletedTask;
            }
            """, Config).RunAsync();

    [Fact]
    public Task SafeUsingInWorkflowFile_DoesNotReport()
        => Create("""
            using System.Text;

            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run() => System.Threading.Tasks.Task.CompletedTask;
            }
            """, Config).RunAsync();

    [Fact]
    public Task UnsafeUsingInNonWorkflowFile_DoesNotReport()
        => Create("""
            using System.IO;

            public class PlainHelper
            {
                public void Run() { }
            }
            """, Config).RunAsync();

    [Fact]
    public Task NoConfig_DoesNotReport()
        => Create("""
            using System.IO;

            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run() => System.Threading.Tasks.Task.CompletedTask;
            }
            """, "root = true\n").RunAsync();
}
