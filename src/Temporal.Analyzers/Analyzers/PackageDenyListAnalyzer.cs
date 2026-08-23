using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Kogoshvili.Temporal.Analyzers.Analysis;
using Kogoshvili.Temporal.Analyzers.Diagnostics;

namespace Kogoshvili.Temporal.Analyzers.Analyzers;

/// <summary>
/// Flags <c>using</c> directives for namespaces configured as unsafe for
/// workflow code (TMP2147). The rule is off by default and driven by the
/// <c>kogoshvili.temporal.unsafe_namespaces</c> config key, so it reports
/// nothing unless the user opts in.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PackageDenyListAnalyzer : DiagnosticAnalyzer
{
    private const string UnsafeNamespacesKey = "kogoshvili.temporal.unsafe_namespaces";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.UnsafeNamespaceReference);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeUsingDirective, SyntaxKind.UsingDirective);
    }

    private static void AnalyzeUsingDirective(SyntaxNodeAnalysisContext context)
    {
        var usingDirective = (UsingDirectiveSyntax)context.Node;
        var tree = usingDirective.SyntaxTree;
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree);
        var prefixes = ReadUnsafeNamespaces(options);
        if (prefixes.Count == 0 || !IsWorkflowFile(context.SemanticModel, tree, options))
        {
            return;
        }

        if (usingDirective.Name is not { } name)
        {
            return;
        }

        var namespaceName = name.ToString();
        if (!prefixes.Any(p => Matches(p, namespaceName)))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.UnsafeNamespaceReference,
            name.GetLocation(),
            namespaceName));
    }

    private static bool IsWorkflowFile(SemanticModel model, SyntaxTree tree, AnalyzerConfigOptions options)
    {
        if (CompilationAnalysisState.ReadWorkflowPathGlobs(options).Any(g => PathGlob.IsMatch(g, tree.FilePath)))
        {
            return true;
        }

        var root = tree.GetRoot();
        foreach (var typeDeclaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            var symbol = model.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;
            if (symbol is not null && WorkflowDetection.IsWorkflowType(symbol))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> ReadUnsafeNamespaces(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(UnsafeNamespacesKey, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .ToList();
    }

    private static bool Matches(string prefix, string name) =>
        name.Equals(prefix, StringComparison.Ordinal) ||
        name.StartsWith(prefix + ".", StringComparison.Ordinal);
}
