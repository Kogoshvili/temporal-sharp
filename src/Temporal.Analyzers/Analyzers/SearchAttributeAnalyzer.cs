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
/// Flags workflow input fields that map (by convention) to a search attribute
/// but are never upserted (TMP2161, opt-in). The field-alias→attribute mapping is
/// supplied via <c>kogoshvili.temporal.search_attributes</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SearchAttributeAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.SearchAttributeNotUpserted,
            DiagnosticDescriptors.UpsertInLoop,
            DiagnosticDescriptors.SearchAttributeUnsetShape);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var config = TemporalConfig.From(startContext.Options.AnalyzerConfigOptionsProvider);
            var state = CompilationAnalysisState.Get(startContext.Compilation, startContext.Options);

            var required = new ConcurrentBag<(IMethodSymbol Method, Location Location, string Field, string Attribute)>();
            var upserted = new ConcurrentDictionary<IMethodSymbol, ConcurrentDictionary<string, byte>>(SymbolEqualityComparer.Default);

            startContext.RegisterSymbolAction(
                c => AnalyzeWorkflowRun(c, config, required),
                SymbolKind.Method);

            startContext.RegisterSyntaxNodeAction(
                c => CollectUpsert(c, state, upserted),
                SyntaxKind.InvocationExpression);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeUpsertInLoop(c, state),
                SyntaxKind.InvocationExpression);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeUnsetShape(c, state),
                SyntaxKind.InvocationExpression);

            startContext.RegisterCompilationEndAction(endContext => Report(endContext, required, upserted));
        });
    }

    // TMP2162 — UpsertTypedSearchAttributes inside a loop.
    private static void AnalyzeUpsertInLoop(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsUpsertCall(context, invocation))
        {
            return;
        }

        if (!state.IsWorkflowReachable(invocation, context.SemanticModel))
        {
            return;
        }

        if (invocation.Ancestors().Any(a =>
                a is ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.UpsertInLoop,
                invocation.GetLocation()));
        }
    }

    // TMP2163 — ValueSet(null) used to remove a search attribute.
    private static void AnalyzeUnsetShape(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
            method.Name != "ValueSet")
        {
            return;
        }

        if (invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is not
            LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression })
        {
            return;
        }

        if (!invocation.Ancestors().OfType<InvocationExpressionSyntax>().Any(a => IsUpsertCall(context, a)))
        {
            return;
        }

        if (!state.IsWorkflowReachable(invocation, context.SemanticModel))
        {
            return;
        }

        var location = (invocation.Expression as MemberAccessExpressionSyntax)?.Name.GetLocation()
                       ?? invocation.GetLocation();

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.SearchAttributeUnsetShape,
            location));
    }

    private static bool IsUpsertCall(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
        {
            return false;
        }

        return method.Name == "UpsertTypedSearchAttributes" &&
               method.ContainingType?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) == SdkNames.WorkflowType;
    }

    private static void AnalyzeWorkflowRun(
        SymbolAnalysisContext context,
        TemporalConfig config,
        ConcurrentBag<(IMethodSymbol Method, Location Location, string Field, string Attribute)> required)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (!WorkflowDetection.IsWorkflowRunMethod(method))
        {
            return;
        }

        var tree = method.DeclaringSyntaxReferences.Length > 0
            ? method.DeclaringSyntaxReferences[0].SyntaxTree
            : null;
        var map = config.SearchAttributes(tree);
        if (map.Count == 0)
        {
            return;
        }

        foreach (var parameter in method.Parameters)
        {
            // Only user-defined DTOs carry search-attribute-mapped fields.
            if (parameter.Type is not INamedTypeSymbol named ||
                named.DeclaringSyntaxReferences.Length == 0)
            {
                continue;
            }

            foreach (var member in named.GetMembers())
            {
                if (member is not IPropertySymbol { DeclaredAccessibility: Accessibility.Public } property ||
                    property.GetMethod is null)
                {
                    continue;
                }

                var alias = TemporalConfig.NormalizeName(property.Name);
                if (!map.TryGetValue(alias, out var attribute))
                {
                    continue;
                }

                var location = property.Locations.Length > 0 ? property.Locations[0] : method.Locations[0];
                required.Add((method, location, property.Name, attribute));
            }
        }
    }

    private static void CollectUpsert(
        SyntaxNodeAnalysisContext context,
        CompilationAnalysisState state,
        ConcurrentDictionary<IMethodSymbol, ConcurrentDictionary<string, byte>> upserted)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (method.Name != "UpsertTypedSearchAttributes" ||
            method.ContainingType?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) != SdkNames.WorkflowType)
        {
            return;
        }

        if (!state.IsWorkflowReachable(invocation, context.SemanticModel))
        {
            return;
        }

        var runMethod = GetOwningRunMethod(invocation, context.SemanticModel);
        if (runMethod is null)
        {
            return;
        }

        var perMethod = upserted.GetOrAdd(
            runMethod,
            _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));

        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            foreach (var keywordCall in argument.Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
            {
                // Only the attribute name passed to ForKeyword marks an upsert;
                // value strings (e.g. ValueSet("...")) must not count, otherwise a
                // value equal to a mapped attribute name could suppress a report.
                if (keywordCall.Expression is not MemberAccessExpressionSyntax
                    {
                        Name.Identifier.ValueText: "ForKeyword",
                    })
                {
                    continue;
                }

                var nameArgument = keywordCall.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
                if (nameArgument is LiteralExpressionSyntax literal &&
                    literal.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    perMethod.TryAdd(literal.Token.ValueText, 0);
                }
            }
        }
    }

    private static void Report(
        CompilationAnalysisContext context,
        ConcurrentBag<(IMethodSymbol Method, Location Location, string Field, string Attribute)> required,
        ConcurrentDictionary<IMethodSymbol, ConcurrentDictionary<string, byte>> upserted)
    {
        foreach (var (method, location, field, attribute) in required)
        {
            if (upserted.TryGetValue(method, out var perMethod) && perMethod.ContainsKey(attribute))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.SearchAttributeNotUpserted,
                location,
                field,
                attribute));
        }
    }

    /// <summary>
    /// Maps an upsert call to the workflow run method that owns it, so the
    /// "never upserted" check is scoped per workflow instead of compilation-wide.
    /// </summary>
    private static IMethodSymbol? GetOwningRunMethod(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        var enclosing = SymbolUtilities.GetEnclosingRegularMethod(model.GetEnclosingSymbol(invocation.SpanStart));
        if (enclosing is null)
        {
            return null;
        }

        if (WorkflowDetection.IsWorkflowRunMethod(enclosing))
        {
            return enclosing;
        }

        var type = enclosing.ContainingType;
        if (type is null || !WorkflowDetection.IsWorkflowType(type))
        {
            return null;
        }

        foreach (var member in type.GetMembers())
        {
            if (member is IMethodSymbol candidate && WorkflowDetection.IsWorkflowRunMethod(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
