using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using TemporalSharp.Analyzers.Analysis;
using TemporalSharp.Analyzers.Diagnostics;

namespace TemporalSharp.Analyzers.Analyzers;

/// <summary>
/// Validates the Temporal SDK workflow-entry-method contract (TMP3201): a
/// [WorkflowRun] method must be public, return Task, be declared in a [Workflow]
/// class, and be the only [WorkflowRun] method in that class.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WorkflowContractAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.InvalidWorkflowRun);

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
                    Report(symbolContext, location, "[WorkflowRun] must be declared in a [Workflow] class");
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

    private static bool IsTaskReturning(IMethodSymbol method) =>
        TypeNames.FullName(method.ReturnType) == "System.Threading.Tasks.Task";

    private static void Report(SymbolAnalysisContext context, Location location, string reason) =>
        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.InvalidWorkflowRun, location, reason));

    private static void Report(CompilationAnalysisContext context, Location location, string reason) =>
        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.InvalidWorkflowRun, location, reason));

    private static Location FirstLocation(IMethodSymbol method) =>
        method.Locations.Length > 0 ? method.Locations[0] : Location.None;
}
