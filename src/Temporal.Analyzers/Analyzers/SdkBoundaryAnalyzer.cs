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
/// code (TMP3212), use of internal <c>Temporalio.*</c> namespaces (TMP2146),
/// standalone-activity client APIs invoked from workflow code (TMP3213), and
/// <c>Workflow.Unsafe</c> members used from workflow code (TMP2148).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SdkBoundaryAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> InternalNamespacePrefixes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Temporalio.Bridge");

    private const string WorkflowUnsafeType = "Temporalio.Workflows.Workflow.Unsafe";

    private static readonly ImmutableHashSet<string> StandaloneActivityClientTypes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Temporalio.Client.ITemporalClient",
        "Temporalio.Client.TemporalClient");

    private static readonly ImmutableHashSet<string> StandaloneActivityMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ExecuteActivityAsync",
        "StartActivityAsync",
        "GetActivityHandle",
        "GetAsyncActivityHandle");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.ClientOrWorkerTypeInWorkflow,
            DiagnosticDescriptors.InternalTemporalNamespace,
            DiagnosticDescriptors.StandaloneActivityInWorkflow,
            DiagnosticDescriptors.WorkflowUnsafeUsage);

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

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeStandaloneActivityInvocation(c, state),
                SyntaxKind.InvocationExpression);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeUnsafeMemberAccess(c, state),
                SyntaxKind.SimpleMemberAccessExpression);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeUnsafeBareIdentifier(c, state),
                SyntaxKind.IdentifierName);
        });
    }

    // TMP3213 — standalone activity client APIs invoked from workflow code.
    private static void AnalyzeStandaloneActivityInvocation(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
            method.ContainingType is null ||
            !StandaloneActivityClientTypes.Contains(TypeNames.FullName(method.ContainingType)) ||
            !StandaloneActivityMethods.Contains(method.Name))
        {
            return;
        }

        if (!state.IsWorkflowReachable(invocation, context.SemanticModel))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.StandaloneActivityInWorkflow,
            invocation.GetLocation(),
            method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    // TMP2148 — Workflow.Unsafe members accessed from workflow-reachable code.
    private static void AnalyzeUnsafeMemberAccess(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;
        var symbol = context.SemanticModel.GetSymbolInfo(memberAccess).Symbol;
        ReportIfWorkflowUnsafe(context, state, symbol, memberAccess, memberAccess.Name.GetLocation());
    }

    // TMP2148 — bare Workflow.Unsafe members via 'using static', e.g. IsReplaying.
    private static void AnalyzeUnsafeBareIdentifier(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var identifier = (IdentifierNameSyntax)context.Node;
        if (identifier.Parent is MemberAccessExpressionSyntax { Name: var name } && name == identifier)
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(identifier).Symbol;
        ReportIfWorkflowUnsafe(context, state, symbol, identifier, identifier.GetLocation());
    }

    private static void ReportIfWorkflowUnsafe(
        SyntaxNodeAnalysisContext context,
        CompilationAnalysisState state,
        ISymbol? symbol,
        SyntaxNode node,
        Location location)
    {
        if (symbol is not (IPropertySymbol or IMethodSymbol or IFieldSymbol or IEventSymbol) ||
            symbol.ContainingType is null ||
            // TypeNames.FullName only composes namespace + name, so a nested
            // type's containing-type chain must be rendered via display format.
            symbol.ContainingType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) != WorkflowUnsafeType ||
            !state.IsWorkflowReachable(node, context.SemanticModel))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.WorkflowUnsafeUsage,
            location,
            symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
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
