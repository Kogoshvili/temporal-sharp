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
        ImmutableArray.Create(DiagnosticDescriptors.SearchAttributeNotUpserted);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var config = TemporalConfig.From(startContext.Options.AnalyzerConfigOptionsProvider);
            var state = CompilationAnalysisState.Get(startContext.Compilation, startContext.Options);

            var required = new ConcurrentBag<(Location Location, string Field, string Attribute)>();
            var upserted = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

            startContext.RegisterSymbolAction(
                c => AnalyzeWorkflowRun(c, config, required),
                SymbolKind.Method);

            startContext.RegisterSyntaxNodeAction(
                c => CollectUpsert(c, state, upserted),
                SyntaxKind.InvocationExpression);

            startContext.RegisterCompilationEndAction(endContext => Report(endContext, required, upserted));
        });
    }

    private static void AnalyzeWorkflowRun(
        SymbolAnalysisContext context,
        TemporalConfig config,
        ConcurrentBag<(Location Location, string Field, string Attribute)> required)
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
        }        foreach (var parameter in method.Parameters)
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
                required.Add((location, property.Name, attribute));
            }
        }
    }

    private static void CollectUpsert(
        SyntaxNodeAnalysisContext context,
        CompilationAnalysisState state,
        ConcurrentDictionary<string, byte> upserted)
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

        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            foreach (var literal in argument.Expression.DescendantNodesAndSelf().OfType<LiteralExpressionSyntax>())
            {
                if (literal.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    upserted.TryAdd(literal.Token.ValueText, 0);
                }
            }
        }
    }

    private static void Report(
        CompilationAnalysisContext context,
        ConcurrentBag<(Location Location, string Field, string Attribute)> required,
        ConcurrentDictionary<string, byte> upserted)
    {
        foreach (var (location, field, attribute) in required)
        {
            if (upserted.ContainsKey(attribute))
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
}
