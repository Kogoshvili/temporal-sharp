using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Analyzers.Analyzers;

namespace Kogoshvili.Temporal.Analyzers.Tests;

public class WorkflowPathDetectionTests
{
    private const string EditorConfig = """
        root = true

        [*.cs]
        kogoshvili.temporal.workflow_paths = **/Workflows/**
        """;

    [Fact]
    public Task NonAnnotatedTypeUnderWorkflowsPath_IsAnalyzed()
    {
        var test = new CSharpAnalyzerTest<DeterminismAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.Sources.Add(("/0/Stubs.cs", TestStubs.Attributes + TestStubs.Sdk));
        test.TestState.Sources.Add(("/0/Workflows/MyWorkflow.cs", """
            public class Helper
            {
                public System.DateTime GetNow() => {|TMP0101:System.DateTime.Now|};
            }
            """));
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", EditorConfig));
        return test.RunAsync();
    }

    [Fact]
    public Task NonAnnotatedTypeOutsideWorkflowsPath_IsNotAnalyzed()
    {
        var test = new CSharpAnalyzerTest<DeterminismAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.Sources.Add(("/0/Stubs.cs", TestStubs.Attributes + TestStubs.Sdk));
        test.TestState.Sources.Add(("/0/Helpers/MyWorkflow.cs", """
            public class Helper
            {
                public System.DateTime GetNow() => System.DateTime.Now;
            }
            """));
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", EditorConfig));
        return test.RunAsync();
    }

    [Fact]
    public Task AnnotatedTypeOutsideWorkflowsPath_StillAnalyzed()
    {
        var test = new CSharpAnalyzerTest<DeterminismAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.Sources.Add(("/0/Stubs.cs", TestStubs.Attributes + TestStubs.Sdk));
        test.TestState.Sources.Add(("/0/Helpers/MyWorkflow.cs", """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run() => System.Threading.Tasks.Task.CompletedTask;

                public System.DateTime GetNow() => {|TMP0101:System.DateTime.Now|};
            }
            """));
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", EditorConfig));
        return test.RunAsync();
    }
}
