using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Kogoshvili.Temporal.Cli.Analysis;
using Kogoshvili.Temporal.Cli.Reporting;

namespace Kogoshvili.Temporal.Cli.Tests;

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

    private static async Task<ImmutableArray<Diagnostic>> Analyze(
        string source,
        AnalyzerOptions? options = null,
        IReadOnlyDictionary<string, DiagnosticSeverity>? severityOverrides = null)
    {
        var references = await ReferenceAssemblies.Net.Net80.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var compilation = CSharpCompilation.Create(
            "Test",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return await AnalysisRunner.AnalyzeCompilationAsync(compilation, options, CancellationToken.None, severityOverrides);
    }

    private static AnalyzerOptions EditorConfig(string key, string value) =>
        new(ImmutableArray<AdditionalText>.Empty, new DictionaryConfigProvider(new Dictionary<string, string>(StringComparer.Ordinal) { [key] = value }));

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
        Assert.Contains("\"severity\": \"error\"", json);
    }

    [Fact]
    public async Task SarifReporter_ProducesValidSarif()
    {
        var diagnostics = await Analyze(WorkflowSource);
        var sarif = Reporter.ToSarif(diagnostics);
        Assert.Contains("\"version\": \"2.1.0\"", sarif);
        Assert.Contains("\"ruleId\": \"TMP0101\"", sarif);
        Assert.Contains("\"level\": \"error\"", sarif);
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
    public void OptionsParse_SeverityOverrideNone()
    {
        var options = Options.Parse(new[] { "x.csproj", "--severity", "TMP0101=none" });
        Assert.Equal(DiagnosticSeverity.Hidden, options.SeverityOverrides["TMP0101"]);
    }

    [Fact]
    public void OptionsParse_AnalyzeSubcommand()
    {
        var options = Options.Parse(new[] { "analyze", "x.csproj" });
        Assert.Equal("x.csproj", options.Path);
    }

    [Fact]
    public async Task SeverityOverride_AffectsFailOn()
    {
        var diagnostics = await Analyze(WorkflowSource); // TMP0101 is error by default
        Assert.Equal(1, Program.ComputeExitCode(diagnostics, DiagnosticSeverity.Error));

        var downgraded = new Dictionary<string, DiagnosticSeverity> { ["TMP0101"] = DiagnosticSeverity.Warning };
        Assert.Equal(0, Program.ComputeExitCode(diagnostics, DiagnosticSeverity.Error, downgraded));
    }

    [Fact]
    public async Task PragmaWarningDisable_SuppressesDiagnostic()
    {
        var source = Stubs + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run()
                {
            #pragma warning disable TMP0101
                    var now = System.DateTime.Now;
            #pragma warning restore TMP0101
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """;
        var diagnostics = await Analyze(source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "TMP0101");
    }

    [Fact]
    public async Task EditorConfigSeverityNone_SuppressesDiagnostic()
    {
        var diagnostics = await Analyze(WorkflowSource, EditorConfig("dotnet_diagnostic.TMP0101.severity", "none"));
        Assert.DoesNotContain(diagnostics, d => d.Id == "TMP0101");
    }

    [Fact]
    public async Task EditorConfigSeverity_EnablesOptInRule()
    {
        var source = Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run(object value) => System.Threading.Tasks.Task.CompletedTask;
            }
            """;
        var diagnostics = await Analyze(source, EditorConfig("dotnet_diagnostic.TMP2171.severity", "warning"));
        Assert.Contains(diagnostics, d => d.Id == "TMP2171");
    }

    [Fact]
    public async Task OptInRule_NotReported_ByDefault()
    {
        var source = Stubs + """
            [Temporalio.Workflows.Workflow]
            public class W
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task Run(object value) => System.Threading.Tasks.Task.CompletedTask;
            }
            """;
        var diagnostics = await Analyze(source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "TMP2171");
    }

    private sealed class DictionaryConfigProvider : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider
    {
        private readonly Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions _options;

        public DictionaryConfigProvider(IReadOnlyDictionary<string, string> values)
            => _options = new DictionaryConfigOptions(values);

        public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GlobalOptions => _options;

        public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

        public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
    }

    private sealed class DictionaryConfigOptions : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions
    {
        private readonly IReadOnlyDictionary<string, string> _values;

        public DictionaryConfigOptions(IReadOnlyDictionary<string, string> values) => _values = values;

        public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value!);
    }
}
