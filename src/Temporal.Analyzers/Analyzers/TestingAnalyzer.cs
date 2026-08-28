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
/// Flags missing testing coverage around Temporal workflows and test
/// environments. This is a POC-level analyzer: it matches Temporal testing types
/// by name (no SDK reference) and deliberately does not distinguish test
/// projects from production projects.
///
/// <list type="bullet">
/// <item>TMP5001 — a compilation declares [Workflow] types but never references
/// <c>WorkflowReplayer</c>, so no replay test covers them.</item>
/// <item>TMP5002 — a <c>WorkflowEnvironment</c>/<c>TestWorkflowEnvironment</c>
/// local is neither scoped with <c>using</c>/<c>await using</c> nor disposed via
/// <c>Dispose</c>/<c>DisposeAsync</c>/<c>ShutdownAsync</c>.</item>
/// <item>TMP5003 — a <c>WorkflowEnvironment</c> is used but no
/// <c>TemporalWorker.ExecuteAsync</c> scoping is found, so the worker never
/// actually runs workflows.</item>
/// </list>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestingAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.MissingReplayTest,
            DiagnosticDescriptors.EnvironmentNotTornDown,
            DiagnosticDescriptors.WorkerNotScoped);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var state = new TestingState();

            startContext.RegisterSymbolAction(
                c => CollectWorkflowType(c, state),
                SymbolKind.NamedType);

            startContext.RegisterSyntaxNodeAction(
                c => MarkReplayerSeen(c, state),
                SyntaxKind.ObjectCreationExpression);

            startContext.RegisterSyntaxNodeAction(
                c => MarkWorkerExecuteAsyncSeen(c, state),
                SyntaxKind.InvocationExpression);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeEnvironmentUsage(c, state),
                SyntaxKind.VariableDeclarator);

            startContext.RegisterCompilationEndAction(
                c => ReportCompilationWide(c, state));
        });
    }

    private static void CollectWorkflowType(SymbolAnalysisContext context, TestingState state)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (!WorkflowDetection.IsWorkflowType(type))
        {
            return;
        }

        state.AddWorkflowType(type, FirstLocation(type));
    }

    private static void MarkReplayerSeen(SyntaxNodeAnalysisContext context, TestingState state)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;
        if (GetRightmostTypeName(creation.Type) == "WorkflowReplayer")
        {
            state.MarkReplayerSeen();
        }
    }

    private static string? GetRightmostTypeName(TypeSyntax? type)
    {
        while (type is QualifiedNameSyntax qualified)
        {
            type = qualified.Right;
        }

        if (type is GenericNameSyntax generic)
        {
            return generic.Identifier.ValueText;
        }

        return (type as IdentifierNameSyntax)?.Identifier.ValueText;
    }

    private static void MarkWorkerExecuteAsyncSeen(SyntaxNodeAnalysisContext context, TestingState state)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol method &&
            IsWorkerExecuteAsync(method))
        {
            state.MarkWorkerExecuteAsyncSeen();
        }
    }

    private static void AnalyzeEnvironmentUsage(SyntaxNodeAnalysisContext context, TestingState state)
    {
        var declarator = (VariableDeclaratorSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declarator) is not ILocalSymbol local ||
            local.Type is not { } type ||
            !IsWorkflowEnvironmentType(type))
        {
            return;
        }

        var name = declarator.Identifier.ValueText;
        var location = declarator.Identifier.GetLocation();
        state.AddEnvironmentUsage(name, location);

        // TMP5002 — the environment is not torn down. `using`/`await using`
        // (declaration or statement form) or an explicit Dispose/DisposeAsync/
        // ShutdownAsync call elsewhere in the enclosing method counts as teardown.
        if (IsScopedByUsing(declarator) || IsDisposed(context, declarator, local))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.EnvironmentNotTornDown,
            location,
            name));
    }

    private static bool IsWorkflowEnvironmentType(ITypeSymbol type) =>
        type.Name is "WorkflowEnvironment" or "TestWorkflowEnvironment";

    private static bool IsWorkerExecuteAsync(IMethodSymbol method) =>
        method.Name == "ExecuteAsync" &&
        method.ContainingType?.Name == "TemporalWorker";

    private static bool IsScopedByUsing(VariableDeclaratorSyntax declarator)
    {
        if (declarator.FirstAncestorOrSelf<VariableDeclarationSyntax>() is { } declaration)
        {
            // `using var env = ...` and `await using var env = ...`.
            if (declaration.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>() is { } statement &&
                statement.UsingKeyword != default)
            {
                return true;
            }
        }

        // `using (var env = ...) { }`.
        return declarator.FirstAncestorOrSelf<UsingStatementSyntax>() is { Declaration: { } usingDeclaration } &&
               usingDeclaration.Variables.Contains(declarator);
    }

    private static bool IsDisposed(
        SyntaxNodeAnalysisContext context,
        VariableDeclaratorSyntax declarator,
        ILocalSymbol local)
    {
        var enclosing = SymbolUtilities.GetEnclosingRegularMethod(
            context.SemanticModel.GetEnclosingSymbol(declarator.SpanStart));
        if (enclosing is null)
        {
            return false;
        }

        foreach (var reference in enclosing.DeclaringSyntaxReferences)
        {
            foreach (var invocation in reference.GetSyntax().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax
                    {
                        Name.Identifier.ValueText: "Dispose" or "DisposeAsync" or "ShutdownAsync",
                    } memberAccess)
                {
                    continue;
                }

                if (SymbolEqualityComparer.Default.Equals(
                        context.SemanticModel.GetSymbolInfo(memberAccess.Expression).Symbol,
                        local))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void ReportCompilationWide(CompilationAnalysisContext context, TestingState state)
    {
        // TMP5001 — a [Workflow] type with no WorkflowReplayer reference anywhere.
        if (!state.ReplayerSeen && state.WorkflowTypes.TryTake(out var workflowType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MissingReplayTest,
                workflowType.Location,
                workflowType.Type.Name));
        }

        // TMP5003 — an environment is used but no worker.ExecuteAsync scoping is
        // found, so the worker never actually runs workflows.
        if (!state.WorkerExecuteAsyncSeen && state.EnvironmentUsages.TryTake(out var environmentUsage))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.WorkerNotScoped,
                environmentUsage.Location));
        }
    }

    private static Location FirstLocation(ISymbol symbol) =>
        symbol.Locations.Length > 0 ? symbol.Locations[0] : Location.None;

    private sealed class TestingState
    {
        private readonly ConcurrentBag<(INamedTypeSymbol Type, Location Location)> _workflowTypes = new();
        private readonly ConcurrentBag<(string Name, Location Location)> _environmentUsages = new();
        private int _replayerSeen;
        private int _workerExecuteAsyncSeen;

        public ConcurrentBag<(INamedTypeSymbol Type, Location Location)> WorkflowTypes => _workflowTypes;

        public ConcurrentBag<(string Name, Location Location)> EnvironmentUsages => _environmentUsages;

        public bool ReplayerSeen => Volatile.Read(ref _replayerSeen) != 0;

        public bool WorkerExecuteAsyncSeen => Volatile.Read(ref _workerExecuteAsyncSeen) != 0;

        public void AddWorkflowType(INamedTypeSymbol type, Location location) =>
            _workflowTypes.Add((type, location));

        public void AddEnvironmentUsage(string name, Location location) =>
            _environmentUsages.Add((name, location));

        public void MarkReplayerSeen() => Volatile.Write(ref _replayerSeen, 1);

        public void MarkWorkerExecuteAsyncSeen() => Volatile.Write(ref _workerExecuteAsyncSeen, 1);
    }
}
