using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Kogoshvili.Temporal.Analyzers.Analysis;
using Kogoshvili.Temporal.Analyzers.Diagnostics;

namespace Kogoshvili.Temporal.Analyzers.Analyzers;

/// <summary>
/// Validates the Temporal SDK contract for workflow query and signal handlers:
/// queries must be synchronous and read-only (TMP3204, TMP3206, TMP3207) and
/// signals must return void or Task (TMP3205).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WorkflowMessageAnalyzer : DiagnosticAnalyzer
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

    // Workflow command/async APIs that a query handler must not invoke.
    private static readonly ImmutableHashSet<string> WorkflowCommandMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "DelayAsync",
        "WaitConditionAsync",
        "ExecuteActivityAsync",
        "ExecuteLocalActivityAsync",
        "ExecuteChildWorkflowAsync",
        "StartChildWorkflowAsync",
        "CreateContinueAsNewException",
        "UpsertTypedSearchAttributes",
        "Patched",
        "DeprecatePatch",
        "SignalExternalWorkflowAsync");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.InvalidQuery,
            DiagnosticDescriptors.InvalidSignal,
            DiagnosticDescriptors.QueryMutation,
            DiagnosticDescriptors.WorkflowApiInQuery,
            DiagnosticDescriptors.MessageNameNotLiteral);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);

        context.RegisterSyntaxNodeAction(AnalyzeAssignment, AssignmentKinds);
        context.RegisterSyntaxNodeAction(AnalyzeIncrementDecrement, IncrementDecrementKinds);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeMessageAttribute, SyntaxKind.Attribute);
    }

    private static void AnalyzeMessageAttribute(SyntaxNodeAnalysisContext context)
    {
        var attribute = (AttributeSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(attribute).Symbol is not IMethodSymbol ctor)
        {
            return;
        }

        var kind = MessageKind(ctor.ContainingType);
        if (kind is null)
        {
            return;
        }

        var name = NameExpression(attribute);
        if (name is null || name.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.MessageNameNotLiteral,
            name.GetLocation(),
            kind));
    }

    private static string? MessageKind(INamedTypeSymbol? attributeClass)
    {
        if (attributeClass is null)
        {
            return null;
        }

        return attributeClass.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) switch
        {
            WorkflowDetection.WorkflowQueryAttributeName => "query",
            WorkflowDetection.WorkflowSignalAttributeName => "signal",
            WorkflowDetection.WorkflowUpdateAttributeName => "update",
            _ => null,
        };
    }

    private static ExpressionSyntax? NameExpression(AttributeSyntax attribute)
    {
        foreach (var argument in attribute.ArgumentList?.Arguments ?? default)
        {
            if (argument.NameEquals?.Name.Identifier.ValueText == "Name")
            {
                return argument.Expression;
            }
        }

        if (attribute.ArgumentList?.Arguments.Count == 1 &&
            attribute.ArgumentList.Arguments[0].NameEquals is null)
        {
            return attribute.ArgumentList.Arguments[0].Expression;
        }

        return null;
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        var location = FirstLocation(method);

        if (WorkflowDetection.IsWorkflowQueryMethod(method))
        {
            AnalyzeQuery(context, method, location);
        }
        else if (WorkflowDetection.IsWorkflowSignalMethod(method))
        {
            AnalyzeSignal(context, method, location);
        }
    }

    private static void AnalyzeQuery(SymbolAnalysisContext context, IMethodSymbol method, Location location)
    {
        if (method.IsAsync)
        {
            Report(context, location, "queries must not be async", DiagnosticDescriptors.InvalidQuery);
        }
        else if (IsNonValueReturn(method.ReturnType))
        {
            Report(context, location, "queries must return a value (not void, Task, or Task<T>)", DiagnosticDescriptors.InvalidQuery);
        }
    }

    private static void AnalyzeSignal(SymbolAnalysisContext context, IMethodSymbol method, Location location)
    {
        if (!IsVoid(method.ReturnType) && !IsNonGenericTask(method.ReturnType))
        {
            Report(context, location, "signals must return void or Task (never Task<T> or a value)", DiagnosticDescriptors.InvalidSignal);
        }
    }

    private static bool IsNonValueReturn(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Void)
        {
            return true;
        }

        return TypeNames.FullName(type) is "System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask";
    }

    private static bool IsVoid(ITypeSymbol type) => type.SpecialType == SpecialType.System_Void;

    private static bool IsNonGenericTask(ITypeSymbol type) =>
        type is INamedTypeSymbol { TypeArguments.Length: 0 } named &&
        TypeNames.FullName(named) == "System.Threading.Tasks.Task";

    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        ReportIfQueryMutation(context, assignment, assignment.Left);
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

        ReportIfQueryMutation(context, context.Node, operand);
    }

    private static void ReportIfQueryMutation(SyntaxNodeAnalysisContext context, SyntaxNode node, ExpressionSyntax target)
    {
        if (GetEnclosingQueryMethod(context, target) is not { } queryMethod)
        {
            return;
        }

        if (!TryGetThisMember(target, context.SemanticModel, out var member))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.QueryMutation,
            node.GetLocation(),
            queryMethod.Name,
            member.Name));
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (method.ContainingType?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) != SdkNames.WorkflowType ||
            !WorkflowCommandMethods.Contains(method.Name))
        {
            return;
        }

        if (GetEnclosingQueryMethod(context, invocation) is null)
        {
            return;
        }

        var display = method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.WorkflowApiInQuery, invocation.GetLocation(), display));
    }

    private static IMethodSymbol? GetEnclosingQueryMethod(SyntaxNodeAnalysisContext context, SyntaxNode node)
    {
        var enclosing = context.SemanticModel.GetEnclosingSymbol(node.SpanStart);
        for (var current = enclosing; current is not null; current = current.ContainingSymbol)
        {
            if (current is IMethodSymbol { MethodKind: not (MethodKind.LambdaMethod or MethodKind.LocalFunction) } method)
            {
                return WorkflowDetection.IsWorkflowQueryMethod(method) ? method : null;
            }
        }

        return null;
    }

    private static bool TryGetThisMember(ExpressionSyntax target, SemanticModel model, out ISymbol member)
    {
        switch (target)
        {
            case IdentifierNameSyntax:
                return TryResolveInstanceMember(model.GetSymbolInfo(target).Symbol, out member);

            case MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax or BaseExpressionSyntax }:
                return TryResolveInstanceMember(model.GetSymbolInfo(target).Symbol, out member);

            default:
                member = null!;
                return false;
        }
    }

    private static bool TryResolveInstanceMember(ISymbol? symbol, out ISymbol member)
    {
        member = symbol!;
        return symbol switch
        {
            IFieldSymbol { IsStatic: false } => true,
            IPropertySymbol { IsStatic: false, SetMethod: not null } => true,
            _ => false,
        };
    }

    private static Location FirstLocation(ISymbol symbol) =>
        symbol.Locations.Length > 0 ? symbol.Locations[0] : Location.None;

    private static void Report(SymbolAnalysisContext context, Location location, string reason, DiagnosticDescriptor descriptor) =>
        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, reason));
}
