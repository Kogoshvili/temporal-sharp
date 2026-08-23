using System.Collections.Concurrent;
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
/// code (TMP3212), <c>StartWorkflowAsync</c> without an explicit workflow id
/// (TMP3213), and use of internal <c>Temporalio.*</c> namespaces (TMP2146).
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
            DiagnosticDescriptors.StartWorkflowWithoutId,
            DiagnosticDescriptors.InternalTemporalNamespace);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var state = CompilationAnalysisState.Get(startContext.Compilation, startContext.Options);
            var startState = new StartWorkflowState();

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeTypeReference(c, state),
                SyntaxKind.IdentifierName);

            startContext.RegisterSyntaxNodeAction(
                c => CollectWorkflowOptions(c, startState),
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ImplicitObjectCreationExpression);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeStartWorkflow(c, startState),
                SyntaxKind.InvocationExpression);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeUsing(c),
                SyntaxKind.UsingDirective);

            startContext.RegisterCompilationEndAction(endContext => ReportStartWorkflow(endContext, startState));
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

    // TMP3213 — StartWorkflowAsync without an explicit workflow id.
    private static void CollectWorkflowOptions(SyntaxNodeAnalysisContext context, StartWorkflowState state)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        var type = context.SemanticModel.GetTypeInfo(creation).Type;
        if (type is null ||
            type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) != SdkNames.WorkflowOptionsType)
        {
            return;
        }

        var hasId = creation.Initializer is { } initializer && InitializerHasId(initializer);

        if (creation.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } &&
            context.SemanticModel.GetDeclaredSymbol(declarator) is { } symbol)
        {
            state.OptionsHasId[symbol] = hasId;
        }
    }

    private static void AnalyzeStartWorkflow(SyntaxNodeAnalysisContext context, StartWorkflowState state)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
            method.Name != "StartWorkflowAsync" ||
            method.ContainingType?.ContainingNamespace.ToDisplayString() != SdkNames.ClientNamespace)
        {
            return;
        }

        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            var type = context.SemanticModel.GetTypeInfo(argument.Expression).Type;
            if (type is null ||
                type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) != SdkNames.WorkflowOptionsType)
            {
                continue;
            }

            var expression = Unwrap(argument.Expression);

            // Options passed inline with an Id initializer.
            if (expression is BaseObjectCreationExpressionSyntax creation)
            {
                if (creation.Initializer is { } initializer && InitializerHasId(initializer))
                {
                    return;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.StartWorkflowWithoutId,
                    invocation.GetLocation()));
                return;
            }

            // Options built in a variable; resolve at compilation end.
            if (expression is IdentifierNameSyntax identifier &&
                context.SemanticModel.GetSymbolInfo(identifier).Symbol is { } symbol)
            {
                state.Pending.Add((invocation.GetLocation(), symbol));
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.StartWorkflowWithoutId,
                invocation.GetLocation()));
            return;
        }
    }

    private static void ReportStartWorkflow(CompilationAnalysisContext context, StartWorkflowState state)
    {
        foreach (var (location, symbol) in state.Pending)
        {
            if (state.OptionsHasId.TryGetValue(symbol, out var hasId) && hasId)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.StartWorkflowWithoutId,
                location));
        }
    }

    private static bool InitializerHasId(InitializerExpressionSyntax initializer) =>
        initializer.Expressions
            .OfType<AssignmentExpressionSyntax>()
            .Any(a => a.Left is IdentifierNameSyntax id && id.Identifier.ValueText == "Id");

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
            if (name.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.InternalTemporalNamespace,
                    usingDirective.Name!.GetLocation(),
                    name));
                return;
            }
        }
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        var current = expression;
        while (current is CastExpressionSyntax cast)
        {
            current = cast.Expression;
        }

        while (current is ParenthesizedExpressionSyntax parens)
        {
            current = parens.Expression;
        }

        return current;
    }

    private sealed class StartWorkflowState
    {
        public ConcurrentDictionary<ISymbol, bool> OptionsHasId { get; } = new(SymbolEqualityComparer.Default);

        public ConcurrentBag<(Location Location, ISymbol Symbol)> Pending { get; } = new();
    }
}
