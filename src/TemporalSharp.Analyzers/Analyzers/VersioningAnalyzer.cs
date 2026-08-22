using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using TemporalSharp.Analyzers.Analysis;
using TemporalSharp.Analyzers.Diagnostics;

namespace TemporalSharp.Analyzers.Analyzers;

/// <summary>
/// Flags workflow versioning (patching) misuse: a patch id that is both
/// Patched and DeprecatePatch'd in the same workflow method (TMP3301), and a
/// patch id that is not a constant string (TMP3302).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class VersioningAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.PatchLeftover,
            DiagnosticDescriptors.NonConstantPatchId);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var state = CompilationAnalysisState.Get(startContext.Compilation, startContext.Options);
            var versioningState = new VersioningState();

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeVersioningCall(c, state, versioningState),
                SyntaxKind.InvocationExpression);

            startContext.RegisterCompilationEndAction(endContext => ReportLeftovers(endContext, versioningState));
        });
    }

    private static void AnalyzeVersioningCall(
        SyntaxNodeAnalysisContext context,
        CompilationAnalysisState state,
        VersioningState versioningState)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (method.ContainingType?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) != SdkNames.WorkflowType)
        {
            return;
        }

        var isPatched = method.Name == "Patched";
        var isDeprecate = method.Name == "DeprecatePatch";
        if (!isPatched && !isDeprecate)
        {
            return;
        }

        if (!state.IsWorkflowReachable(invocation, context.SemanticModel))
        {
            return;
        }

        var argument = invocation.ArgumentList.Arguments.FirstOrDefault();
        if (argument is null)
        {
            return;
        }

        var id = ConstantStringValue(context, argument.Expression);
        if (id is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.NonConstantPatchId,
                invocation.GetLocation(),
                method.Name));
            return;
        }

        var enclosing = SymbolUtilities.GetEnclosingRegularMethod(
            context.SemanticModel.GetEnclosingSymbol(invocation.SpanStart));
        if (enclosing is null)
        {
            return;
        }

        if (isPatched)
        {
            AddId(versioningState.Patched, enclosing, id, invocation.GetLocation());
        }
        else
        {
            AddId(versioningState.Deprecated, enclosing, id, invocation.GetLocation());
        }
    }

    private static void AddId(
        ConcurrentDictionary<IMethodSymbol, ConcurrentDictionary<string, Location>> map,
        IMethodSymbol method,
        string id,
        Location location)
    {
        var ids = map.GetOrAdd(method, _ => new ConcurrentDictionary<string, Location>(StringComparer.Ordinal));
        ids.TryAdd(id, location);
    }

    private static string? ConstantStringValue(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
    {
        if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return literal.Token.ValueText;
        }

        if (expression is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } } nameofInvocation)
        {
            return nameofInvocation.ArgumentList.Arguments.FirstOrDefault()?.ToString();
        }

        if (context.SemanticModel.GetSymbolInfo(expression).Symbol is IFieldSymbol { IsConst: true, ConstantValue: string constValue })
        {
            return constValue;
        }

        return null;
    }

    private static void ReportLeftovers(CompilationAnalysisContext context, VersioningState state)
    {
        foreach (var entry in state.Deprecated)
        {
            var method = entry.Key;
            var deprecatedIds = entry.Value;

            if (!state.Patched.TryGetValue(method, out var patchedIds))
            {
                continue;
            }

            foreach (var idEntry in deprecatedIds)
            {
                if (!patchedIds.ContainsKey(idEntry.Key))
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.PatchLeftover,
                    idEntry.Value,
                    idEntry.Key));
            }
        }
    }

    private sealed class VersioningState
    {
        public ConcurrentDictionary<IMethodSymbol, ConcurrentDictionary<string, Location>> Patched { get; } =
            new(SymbolEqualityComparer.Default);

        public ConcurrentDictionary<IMethodSymbol, ConcurrentDictionary<string, Location>> Deprecated { get; } =
            new(SymbolEqualityComparer.Default);
    }
}
