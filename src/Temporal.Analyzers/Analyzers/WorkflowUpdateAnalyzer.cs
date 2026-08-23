using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Kogoshvili.Temporal.Analyzers.Analysis;
using Kogoshvili.Temporal.Analyzers.Diagnostics;

namespace Kogoshvili.Temporal.Analyzers.Analyzers;

/// <summary>
/// Validates the Temporal SDK contract for workflow update handlers and update
/// validators (TMP3208, TMP3209, TMP3215, TMP3216, TMP3217): updates must return
/// a concrete <c>Task&lt;T&gt;</c>, must not raise continue-as-new, validators
/// must be pure and non-blocking, handlers must not schedule workflow commands,
/// and async handlers must be drained before the workflow completes.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WorkflowUpdateAnalyzer : DiagnosticAnalyzer
{
    private static readonly SyntaxKind[] AssignmentKinds =
    {
        SyntaxKind.SimpleAssignmentExpression,
        SyntaxKind.AddAssignmentExpression,
        SyntaxKind.SubtractAssignmentExpression,
        SyntaxKind.MultiplyAssignmentExpression,
        SyntaxKind.DivideAssignmentExpression,
        SyntaxKind.ModuloAssignmentExpression,
        SyntaxKind.AndAssignmentExpression,
        SyntaxKind.OrAssignmentExpression,
        SyntaxKind.ExclusiveOrAssignmentExpression,
        SyntaxKind.LeftShiftAssignmentExpression,
        SyntaxKind.RightShiftAssignmentExpression,
        SyntaxKind.CoalesceAssignmentExpression,
    };

    private static readonly SyntaxKind[] IncrementDecrementKinds =
    {
        SyntaxKind.PostIncrementExpression,
        SyntaxKind.PreIncrementExpression,
        SyntaxKind.PostDecrementExpression,
        SyntaxKind.PreDecrementExpression,
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.InvalidWorkflowUpdate,
            DiagnosticDescriptors.ContinueAsNewInUpdate,
            DiagnosticDescriptors.UpdateValidatorSideEffect,
            DiagnosticDescriptors.HandlerSchedulesWork,
            DiagnosticDescriptors.CompleteWithPendingHandlers);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(AnalyzeUpdateMethod, SymbolKind.Method);
        context.RegisterSymbolAction(AnalyzeWorkflowType, SymbolKind.NamedType);

        context.RegisterSyntaxNodeAction(AnalyzeAssignment, AssignmentKinds);
        context.RegisterSyntaxNodeAction(AnalyzeIncrementDecrement, IncrementDecrementKinds);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    // TMP3208 — [WorkflowUpdate] must return a Task (or Task<T> for a result).
    private static void AnalyzeUpdateMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (!WorkflowDetection.IsWorkflowUpdateMethod(method))
        {
            return;
        }

        if (IsTaskOrSubtype(method.ReturnType))
        {
            return;
        }

        Report(context, FirstLocation(method), "the update handler must return Task or Task<T>");
    }

    private static bool IsTaskOrSubtype(ITypeSymbol type) =>
        TypeNames.IsOrDerivesFrom(type, "System.Threading.Tasks.Task");

    // TMP3217 — async handlers exist but the workflow never awaits AllHandlersFinished.
    private static void AnalyzeWorkflowType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (!WorkflowDetection.IsWorkflowType(type))
        {
            return;
        }

        var methods = type.GetMembers().OfType<IMethodSymbol>().ToList();

        var hasAsyncHandler = methods.Any(m =>
            (WorkflowDetection.IsWorkflowSignalMethod(m) || WorkflowDetection.IsWorkflowUpdateMethod(m)) &&
            TypeNames.FullName(m.ReturnType) == "System.Threading.Tasks.Task");

        if (!hasAsyncHandler)
        {
            return;
        }

        var run = methods.FirstOrDefault(WorkflowDetection.IsWorkflowRunMethod);
        if (run is null || ReferencesAllHandlersFinished(run))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.CompleteWithPendingHandlers,
            FirstLocation(type),
            type.Name));
    }

    private static bool ReferencesAllHandlersFinished(IMethodSymbol method)
    {
        foreach (var syntaxReference in method.DeclaringSyntaxReferences)
        {
            var node = syntaxReference.GetSyntax();
            foreach (var identifier in node.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (identifier.Identifier.ValueText == "AllHandlersFinished")
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        ReportIfValidatorMutates(context, assignment, assignment.Left);
    }

    private static void AnalyzeIncrementDecrement(SyntaxNodeAnalysisContext context)
    {
        var operand = context.Node switch
        {
            PrefixUnaryExpressionSyntax prefix => prefix.Operand,
            PostfixUnaryExpressionSyntax postfix => postfix.Operand,
            _ => null,
        };

        if (operand is null)
        {
            return;
        }

        ReportIfValidatorMutates(context, context.Node, operand);
    }

    private static void ReportIfValidatorMutates(SyntaxNodeAnalysisContext context, SyntaxNode node, ExpressionSyntax target)
    {
        if (GetEnclosingValidator(context, node) is not { } validator)
        {
            return;
        }

        if (!SymbolUtilities.TryGetMutatedInstanceMember(target, context.SemanticModel, out var member))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.UpdateValidatorSideEffect,
            node.GetLocation(),
            validator.Name,
            $"writes to instance member '{member.Name}'"));
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Update validators must only read state; a mutating call on an instance
        // member (e.g. _items.Add(x)) is a state mutation.
        if (GetEnclosingValidator(context, invocation) is { } mutatingValidator &&
            SymbolUtilities.TryGetMutatedInstanceMember(invocation, context.SemanticModel, out var mutatedMember))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.UpdateValidatorSideEffect,
                invocation.GetLocation(),
                mutatingValidator.Name,
                $"writes to instance member '{mutatedMember.Name}'"));
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
        {
            return;
        }

        var display = method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        // TMP3209 — continue-as-new inside an update handler.
        if (IsWorkflowApi(method, "CreateContinueAsNewException"))
        {
            if (GetEnclosingUpdate(context, invocation) is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ContinueAsNewInUpdate,
                    invocation.GetLocation(),
                    display));
            }

            return;
        }

        if (!SdkNames.IsWorkflowCommand(method))
        {
            return;
        }

        // TMP3216 — a signal/update handler schedules a workflow command.
        if (GetEnclosingHandlerKind(context, invocation) is { } handlerKind)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.HandlerSchedulesWork,
                invocation.GetLocation(),
                display,
                handlerKind));
        }

        // TMP3215 — an update validator schedules a workflow command.
        if (GetEnclosingValidator(context, invocation) is { } validator)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.UpdateValidatorSideEffect,
                invocation.GetLocation(),
                validator.Name,
                "performs blocking work"));
        }
    }

    private static bool IsWorkflowApi(IMethodSymbol method, string name) =>
        method.Name == name &&
        method.ContainingType is not null &&
        SdkNames.IsWorkflowType(method.ContainingType);

    private static IMethodSymbol? GetEnclosingUpdate(SyntaxNodeAnalysisContext context, SyntaxNode node)
    {
        var enclosing = context.SemanticModel.GetEnclosingSymbol(node.SpanStart);
        for (var current = enclosing; current is not null; current = current.ContainingSymbol)
        {
            if (current is IMethodSymbol { MethodKind: not (MethodKind.LambdaMethod or MethodKind.LocalFunction) } method)
            {
                return WorkflowDetection.IsWorkflowUpdateMethod(method) ? method : null;
            }
        }

        return null;
    }

    private static string? GetEnclosingHandlerKind(SyntaxNodeAnalysisContext context, SyntaxNode node)
    {
        var enclosing = context.SemanticModel.GetEnclosingSymbol(node.SpanStart);
        for (var current = enclosing; current is not null; current = current.ContainingSymbol)
        {
            if (current is IMethodSymbol { MethodKind: not (MethodKind.LambdaMethod or MethodKind.LocalFunction) } method)
            {
                if (WorkflowDetection.IsWorkflowSignalMethod(method))
                {
                    return "signal";
                }

                if (WorkflowDetection.IsWorkflowUpdateMethod(method))
                {
                    return "update";
                }

                return null;
            }
        }

        return null;
    }

    private static IMethodSymbol? GetEnclosingValidator(SyntaxNodeAnalysisContext context, SyntaxNode node)
    {
        var enclosing = context.SemanticModel.GetEnclosingSymbol(node.SpanStart);
        for (var current = enclosing; current is not null; current = current.ContainingSymbol)
        {
            if (current is IMethodSymbol { MethodKind: not (MethodKind.LambdaMethod or MethodKind.LocalFunction) } method)
            {
                return WorkflowDetection.IsWorkflowUpdateValidatorMethod(method) ? method : null;
            }
        }

        return null;
    }

    private static Location FirstLocation(ISymbol symbol) =>
        symbol.Locations.Length > 0 ? symbol.Locations[0] : Location.None;

    private static void Report(SymbolAnalysisContext context, Location location, string reason) =>
        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.InvalidWorkflowUpdate, location, reason));
}
