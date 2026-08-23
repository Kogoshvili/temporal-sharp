using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Kogoshvili.Temporal.Analyzers.Analysis;
using Kogoshvili.Temporal.Analyzers.Diagnostics;

namespace Kogoshvili.Temporal.Analyzers.Analyzers;

/// <summary>
/// Validates the Temporal SDK contract for workflow entry methods (TMP3201) and
/// activity declarations (TMP3202): a [WorkflowRun] method must be public, return
/// Task, be declared in a [Workflow] type, and be the only [WorkflowRun] method
/// in that type; the [Activity] attribute may only be applied to methods, and a
/// typed-lambda activity target must be marked [Activity].
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WorkflowContractAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.InvalidWorkflowRun,
            DiagnosticDescriptors.InvalidActivity);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var runMethods = new List<IMethodSymbol>();
            var gate = new object();

            startContext.RegisterSymbolAction(symbolContext =>
            {
                var method = (IMethodSymbol)symbolContext.Symbol;
                if (!WorkflowDetection.IsWorkflowRunMethod(method))
                {
                    return;
                }

                lock (gate)
                {
                    runMethods.Add(method);
                }

                var location = FirstLocation(method);

                if (!WorkflowDetection.IsWorkflowType(method.ContainingType))
                {
                    Report(symbolContext, location, "[WorkflowRun] must be declared in a [Workflow] type");
                }

                if (method.DeclaredAccessibility != Accessibility.Public)
                {
                    Report(symbolContext, location, "the entry method must be public");
                }

                if (!IsTaskReturning(method))
                {
                    Report(symbolContext, location, "the entry method must return Task");
                }
            }, SymbolKind.Method);

            startContext.RegisterSymbolAction(
                AnalyzeActivityOnNonMethod,
                SymbolKind.Field, SymbolKind.Property, SymbolKind.NamedType);

            startContext.RegisterSyntaxNodeAction(
                AnalyzeMissingActivity,
                SyntaxKind.InvocationExpression);

            startContext.RegisterCompilationEndAction(endContext =>
            {
                foreach (var group in runMethods.GroupBy(m => m.ContainingType, SymbolEqualityComparer.Default))
                {
                    var ordered = group
                        .OrderBy(m => m.Locations.Length > 0 ? m.Locations[0].SourceSpan.Start : int.MaxValue)
                        .ToList();

                    foreach (var extra in ordered.Skip(1))
                    {
                        Report(endContext, FirstLocation(extra), "a [Workflow] class may have only one [WorkflowRun] method");
                    }
                }
            });
        });
    }

    private static void AnalyzeActivityOnNonMethod(SymbolAnalysisContext context)
    {
        if (!WorkflowDetection.HasActivityAttribute(context.Symbol))
        {
            return;
        }

        Report(context, FirstLocation(context.Symbol), "the [Activity] attribute may only be applied to a method", DiagnosticDescriptors.InvalidActivity);
    }

    private static void AnalyzeMissingActivity(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (method.ContainingType?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) != SdkNames.WorkflowType ||
            method.Name is not ("ExecuteActivityAsync" or "ExecuteLocalActivityAsync"))
        {
            return;
        }

        var target = LambdaTargetResolver.ResolveTypedLambdaTarget(context, invocation);
        if (target is null || WorkflowDetection.IsActivityMethod(target))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.InvalidActivity,
            invocation.GetLocation(),
            $"target '{target.Name}' must be marked [Activity]"));
    }

    private static bool IsTaskReturning(IMethodSymbol method) =>
        TypeNames.FullName(method.ReturnType) == "System.Threading.Tasks.Task";

    private static void Report(SymbolAnalysisContext context, Location location, string reason) =>
        Report(context, location, reason, DiagnosticDescriptors.InvalidWorkflowRun);

    private static void Report(CompilationAnalysisContext context, Location location, string reason) =>
        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.InvalidWorkflowRun, location, reason));

    private static void Report(SymbolAnalysisContext context, Location location, string reason, DiagnosticDescriptor descriptor) =>
        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, reason));

    private static Location FirstLocation(ISymbol symbol) =>
        symbol.Locations.Length > 0 ? symbol.Locations[0] : Location.None;
}
