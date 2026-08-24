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
        foreach (var prefix in InternalNamespacePrefixes)
        {
            if (name == prefix || name.StartsWith(prefix + ".", System.StringComparison.Ordinal))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.InternalTemporalNamespace,
                    usingDirective.Name!.GetLocation(),
                    name));
                return;
            }
        }
    }
}
