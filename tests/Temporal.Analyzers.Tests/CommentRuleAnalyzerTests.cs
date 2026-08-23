using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Analyzers.Analyzers;

namespace Kogoshvili.Temporal.Analyzers.Tests;

public class CommentRuleAnalyzerTests
{
    private const string Stubs = TestStubs.Attributes + TestStubs.Sdk;

    private static Task VerifyWithRule(string source, string ruleId)
    {
        var test = new CSharpAnalyzerTest<CommentRuleAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.AnalyzerConfigFiles.Add((
            "/.editorconfig",
            $"root = true\n\n[*.cs]\ndotnet_diagnostic.{ruleId}.severity = warning\n"));
        return test.RunAsync();
    }

    [Fact]
    public Task NewGuid_WithoutComment_Reports()
        => VerifyWithRule(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var id = {|TMP4201:Temporalio.Workflows.Workflow.NewGuid()|};
                }
            }
            """, "TMP4201");

    [Fact]
    public Task NewGuid_WithComment_DoesNotReport()
        => VerifyWithRule(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    // deterministic, not for security
                    var id = Temporalio.Workflows.Workflow.NewGuid();
                }
            }
            """, "TMP4201");

    [Fact]
    public Task DeprecatePatch_WithoutComment_Reports()
        => VerifyWithRule(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP4202:Temporalio.Workflows.Workflow.DeprecatePatch("v1")|};
                }
            }
            """, "TMP4202");

    [Fact]
    public Task DeprecatePatch_WithComment_DoesNotReport()
        => VerifyWithRule(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    // removed the old behavior
                    Temporalio.Workflows.Workflow.DeprecatePatch("v1");
                }
            }
            """, "TMP4202");

    [Fact]
    public Task Patched_WithoutReplayComment_Reports()
        => VerifyWithRule(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    if ({|TMP4203:Temporalio.Workflows.Workflow.Patched("v1")|})
                    {
                    }
                }
            }
            """, "TMP4203");

    [Fact]
    public Task Patched_WithReplayComment_DoesNotReport()
        => VerifyWithRule(Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    // replay tested against v1 histories
                    if (Temporalio.Workflows.Workflow.Patched("v1"))
                    {
                    }
                }
            }
            """, "TMP4203");
}
