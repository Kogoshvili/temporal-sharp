using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using TemporalSharp.Analyzers.Analysis;
using TemporalSharp.Analyzers.Diagnostics;

namespace TemporalSharp.Analyzers.Analyzers;

/// <summary>
/// Flags mutation of static state from workflow code, which breaks replay
/// determinism and races across workflow executions.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WorkflowStateAnalyzer : DiagnosticAnalyzer
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
            DiagnosticDescriptors.StaticStateMutation,
            DiagnosticDescriptors.ThreadStaticMutation,
            DiagnosticDescriptors.StaticPropertySetter);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var state = CompilationAnalysisState.Get(startContext.Compilation);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeAssignment(c, state),
                AssignmentKinds);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeIncrementDecrement(c, state),
                IncrementDecrementKinds);
        });
    }

    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        ReportIfStaticMutable(context, state, assignment, assignment.Left);
    }

    private static void AnalyzeIncrementDecrement(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
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

        ReportIfStaticMutable(context, state, context.Node, operand);
    }

    private static void ReportIfStaticMutable(
        SyntaxNodeAnalysisContext context,
        CompilationAnalysisState state,
        SyntaxNode node,
        ExpressionSyntax target)
    {
        var symbol = context.SemanticModel.GetSymbolInfo(target).Symbol;
        var descriptor = GetStaticMutationDescriptor(symbol);
        if (descriptor is null)
        {
            return;
        }

        if (!state.IsWorkflowReachable(node, context.SemanticModel))
        {
            return;
        }

        var display = symbol!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        context.ReportDiagnostic(Diagnostic.Create(descriptor, node.GetLocation(), display));
    }

    private static DiagnosticDescriptor? GetStaticMutationDescriptor(ISymbol? symbol) => symbol switch
    {
        IFieldSymbol { IsStatic: true } field =>
            IsThreadStatic(field)
                ? DiagnosticDescriptors.ThreadStaticMutation
                : DiagnosticDescriptors.StaticStateMutation,
        IPropertySymbol { IsStatic: true } property when property.SetMethod is not null =>
            DiagnosticDescriptors.StaticPropertySetter,
        _ => null,
    };

    private static bool IsThreadStatic(IFieldSymbol field)
    {
        foreach (var attribute in field.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ==
                "System.ThreadStaticAttribute")
            {
                return true;
            }
        }

        return false;
    }
}
