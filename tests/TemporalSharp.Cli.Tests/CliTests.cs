using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using TemporalSharp.Cli.Analysis;
using TemporalSharp.Cli.Reporting;

namespace TemporalSharp.Cli.Tests;

public class CliTests
{
    private const string Stubs = """
        namespace Temporalio.Workflows
        {
            public sealed class WorkflowAttribute : System.Attribute { }
            public sealed class WorkflowRunAttribute : System.Attribute { }
        }
        """;

    private const string WorkflowSource = Stubs + """
        [Temporalio.Workflows.Workflow]
        public class MyWorkflow
        {
            [Temporalio.Workflows.WorkflowRun]
            public System.Threading.Tasks.Task Run()
            {
                var now = System.DateTime.Now;
                return System.Threading.Tasks.Task.CompletedTask;
            }
        }
        """;

    private const string CleanSource = Stubs + """
        [Temporalio.Workflows.Workflow]
        public class MyWorkflow
        {
            [Temporalio.Workflows.WorkflowRun]
            public System.Threading.Tasks.Task Run()
            {
                var x = 1;
                return System.Threading.Tasks.Task.CompletedTask;
            }
        }
        """;

    private static async Task<ImmutableArray<Diagnostic>> Analyze(string source)
    {
        var references = await ReferenceAssemblies.Net.Net80.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var compilation = CSharpCompilation.Create(
            "Test",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return await AnalysisRunner.AnalyzeCompilationAsync(compilation, null, CancellationToken.None);
    }

    [Fact]
    public async Task DeterminismViolation_IsDetected()
    {
        var diagnostics = await Analyze(WorkflowSource);
        Assert.Contains(diagnostics, d => d.Id == "TMP0101");
    }

    [Fact]
    public async Task CleanWorkflow_ProducesNoDiagnostics()
    {
        var diagnostics = await Analyze(CleanSource);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task JsonReporter_ProducesJson()
    {
        var diagnostics = await Analyze(WorkflowSource);
        var json = Reporter.ToJson(diagnostics);
        Assert.Contains("\"id\": \"TMP0101\"", json);
        Assert.Contains("\"severity\": \"warning\"", json);
    }

    [Fact]
    public async Task SarifReporter_ProducesValidSarif()
    {
        var diagnostics = await Analyze(WorkflowSource);
        var sarif = Reporter.ToSarif(diagnostics);
        Assert.Contains("\"version\": \"2.1.0\"", sarif);
        Assert.Contains("\"ruleId\": \"TMP0101\"", sarif);
        Assert.Contains("\"level\": \"warning\"", sarif);
    }

    [Fact]
    public async Task FailOnWarning_WithWarning_ReturnsNonZero()
    {
        var diagnostics = await Analyze(WorkflowSource);
        Assert.Equal(1, Program.ComputeExitCode(diagnostics, DiagnosticSeverity.Warning));
    }

    [Fact]
    public async Task FailOnWarning_WithNoDiagnostics_ReturnsZero()
    {
        var diagnostics = await Analyze(CleanSource);
        Assert.Equal(0, Program.ComputeExitCode(diagnostics, DiagnosticSeverity.Warning));
    }

    [Fact]
    public async Task FailOnNone_WithWarning_ReturnsZero()
    {
        var diagnostics = await Analyze(WorkflowSource);
        Assert.Equal(0, Program.ComputeExitCode(diagnostics, null));
    }

    [Fact]
    public void OptionsParse_RejectsUnknownOption()
    {
        Assert.Throws<ArgumentException>(() => Options.Parse(new[] { "analyze", "x.csproj", "--bogus" }));
    }

    [Fact]
    public void OptionsParse_RequiresPath()
    {
        Assert.Throws<ArgumentException>(() => Options.Parse(new[] { "--format", "json" }));
    }

    [Fact]
    public void OptionsParse_SeverityOverride()
    {
        var options = Options.Parse(new[] { "x.csproj", "--severity", "TMP0101=error" });
        Assert.Equal(DiagnosticSeverity.Error, options.SeverityOverrides["TMP0101"]);
    }

    [Fact]
    public async Task SeverityOverride_EscalatesForFailOn()
    {
        var diagnostics = await Analyze(WorkflowSource);
        var overrides = new Dictionary<string, DiagnosticSeverity> { ["TMP0101"] = DiagnosticSeverity.Error };
        Assert.Equal(1, Program.ComputeExitCode(diagnostics, DiagnosticSeverity.Error, overrides));
        Assert.Equal(0, Program.ComputeExitCode(diagnostics, DiagnosticSeverity.Error));
    }

    [Fact]
    public async Task WorkflowCheckIgnoreComment_SuppressesDiagnostic()
    {
        var source = Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run()
                {
                    var now = System.DateTime.Now; // workflowcheck:ignore
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """;
        var diagnostics = await Analyze(source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "TMP0101");
    }
}
