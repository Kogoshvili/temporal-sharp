using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Kogoshvili.Temporal.Analyzers.Analysis;
using Kogoshvili.Temporal.Analyzers.Diagnostics;

namespace Kogoshvili.Temporal.Analyzers.Analyzers;

/// <summary>
/// Flags SDK-boundary mistakes: client/worker types referenced from workflow
/// code (TMP3212) and use of internal <c>Temporalio.*</c> namespaces (TMP2146).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SdkBoundaryAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> InternalNamespacePrefixes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Temporalio.Bridge");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.ClientOrWorkerTypeInWorkflow,
            DiagnosticDescriptors.InternalTemporalNamespace);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var state = CompilationAnalysisState.Get(startContext.Compilation, startContext.Options);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeTypeReference(c, state),
                SyntaxKind.IdentifierName);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeUsing(c),
                SyntaxKind.UsingDirective);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeQualifiedReference(c),
                SyntaxKind.QualifiedName);
        });
    }

    // TMP3212 — client/worker type referenced from workflow-reachable code.
    private static void AnalyzeTypeReference(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var identifier = (IdentifierNameSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(identifier).Symbol is not INamedTypeSymbol type ||
            !SdkNames.ClientWorkerTypes.Contains(TypeNames.FullName(type)))
        {
            return;
        }

        if (!state.IsWorkflowReachable(identifier, context.SemanticModel))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ClientOrWorkerTypeInWorkflow,
            identifier.GetLocation(),
            type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    // TMP2146 — using an internal Temporalio.* namespace.
    private static void AnalyzeUsing(SyntaxNodeAnalysisContext context)
    {
        var usingDirective = (UsingDirectiveSyntax)context.Node;
        if (usingDirective.Alias is not null)
        {
            return;
        }

        var name = usingDirective.Name?.ToString() ?? string.Empty;
        if (MatchesInternalNamespace(name))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InternalTemporalNamespace,
                usingDirective.Name!.GetLocation(),
                name));
        }
    }

    // TMP2146 — fully-qualified reference to an internal Temporalio.* namespace
    // (e.g. Temporalio.Bridge.Api.*) outside a using/namespace declaration.
    private static void AnalyzeQualifiedReference(SyntaxNodeAnalysisContext context)
    {
        var qualifiedName = (QualifiedNameSyntax)context.Node;

        // Only the topmost name in a chain (not a prefix of a longer name).
        if (qualifiedName.Parent is QualifiedNameSyntax { Left: var left } && left == qualifiedName)
        {
            return;
        }

        foreach (var ancestor in qualifiedName.Ancestors())
        {
            if (ancestor is UsingDirectiveSyntax or NamespaceDeclarationSyntax or FileScopedNamespaceDeclarationSyntax)
            {
                return;
            }
        }

        var name = qualifiedName.ToString();
        if (!MatchesInternalNamespace(name))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.InternalTemporalNamespace,
            qualifiedName.GetLocation(),
            name));
    }

    private static bool MatchesInternalNamespace(string name)
    {
        foreach (var prefix in InternalNamespacePrefixes)
        {
            if (name == prefix || name.StartsWith(prefix + ".", System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
