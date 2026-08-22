using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using TemporalSharp.Analyzers.Analysis;
using TemporalSharp.Analyzers.Diagnostics;

namespace TemporalSharp.Analyzers.Analyzers;

/// <summary>
/// Flags mutation of instance state from <c>[Activity]</c> methods (TMP3203).
/// Activities must be stateless; a shared activity instance's mutable fields and
/// properties race across concurrent executions.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ActivityStateAnalyzer : DiagnosticAnalyzer
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
        ImmutableArray.Create(DiagnosticDescriptors.ActivityInstanceState);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(
            c => AnalyzeAssignment(c),
            AssignmentKinds);

        context.RegisterSyntaxNodeAction(
            c => AnalyzeIncrementDecrement(c),
            IncrementDecrementKinds);
    }

    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        ReportIfInstanceState(context, assignment, assignment.Left);
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

        ReportIfInstanceState(context, context.Node, operand);
    }

    private static void ReportIfInstanceState(
        SyntaxNodeAnalysisContext context,
        SyntaxNode node,
        ExpressionSyntax target)
    {
        if (GetEnclosingActivityMethod(context, target) is not { } activityMethod)
        {
            return;
        }

        if (!TryGetThisMember(target, context.SemanticModel, out var member))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ActivityInstanceState,
            node.GetLocation(),
            activityMethod.Name,
            member.Name));
    }

    private static IMethodSymbol? GetEnclosingActivityMethod(SyntaxNodeAnalysisContext context, SyntaxNode node)
    {
        var enclosing = context.SemanticModel.GetEnclosingSymbol(node.SpanStart);
        for (var current = enclosing; current is not null; current = current.ContainingSymbol)
        {
            if (current is IMethodSymbol { MethodKind: not (MethodKind.LambdaMethod or MethodKind.LocalFunction) } method)
            {
                return WorkflowDetection.IsActivityMethod(method) ? method : null;
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
        return symbol is IFieldSymbol { IsStatic: false } or
               IPropertySymbol { IsStatic: false, SetMethod: not null };
    }
}
