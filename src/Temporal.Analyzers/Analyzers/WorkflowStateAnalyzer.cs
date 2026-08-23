using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Kogoshvili.Temporal.Analyzers.Analysis;
using Kogoshvili.Temporal.Analyzers.Diagnostics;

namespace Kogoshvili.Temporal.Analyzers.Analyzers;

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

    private static readonly ImmutableHashSet<string> MutatingMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Add", "AddRange", "Insert", "InsertRange",
        "Remove", "RemoveAt", "RemoveAll", "RemoveRange",
        "Clear", "TryAdd", "TryRemove", "TryUpdate", "AddOrUpdate", "GetOrAdd",
        "Push", "Pop", "Enqueue", "Dequeue",
        "AddFirst", "AddLast", "RemoveFirst", "RemoveLast",
        "Take", "TryTake");

    private static readonly ImmutableHashSet<string> MutatingVerbs = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Set", "Add", "AddRange", "Insert", "InsertRange",
        "Remove", "RemoveAt", "RemoveAll", "RemoveRange",
        "Clear", "TryAdd", "TryRemove", "TryUpdate", "AddOrUpdate", "GetOrAdd",
        "Push", "Pop", "Enqueue", "Dequeue",
        "AddFirst", "AddLast", "RemoveFirst", "RemoveLast",
        "Take", "TryTake",
        "Create", "CreateEntry", "Update", "Delete", "Write", "WriteAsync",
        "Put", "Save", "Replace", "Reset", "Refresh", "Upsert",
        "Start", "StartAsync", "Stop", "StopAsync", "Dispose", "Flush", "FlushAsync",
        "Register", "Unregister", "Subscribe", "Unsubscribe", "Append", "Extend",
        "Increment", "Decrement");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.StaticStateMutation,
            DiagnosticDescriptors.ThreadStaticMutation,
            DiagnosticDescriptors.StaticPropertySetter,
            DiagnosticDescriptors.StaticCollectionMutation,
            DiagnosticDescriptors.StaticMethodMutation);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var state = CompilationAnalysisState.Get(startContext.Compilation, startContext.Options);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeAssignment(c, state),
                AssignmentKinds);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeIncrementDecrement(c, state),
                IncrementDecrementKinds);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeCollectionMutation(c, state),
                SyntaxKind.InvocationExpression);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeStaticObjectMutation(c, state),
                SyntaxKind.InvocationExpression);
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

    private static void AnalyzeCollectionMutation(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (!MutatingMethods.Contains(memberAccess.Name.Identifier.ValueText))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
            method.IsStatic || method.IsExtensionMethod)
        {
            return;
        }

        var receiver = context.SemanticModel.GetSymbolInfo(memberAccess.Expression).Symbol;
        if (receiver is not IFieldSymbol { IsStatic: true } and not IPropertySymbol { IsStatic: true })
        {
            return;
        }

        if (IsSdkManagedStatic(receiver))
        {
            return;
        }

        var receiverType = (receiver as IFieldSymbol)?.Type ?? ((IPropertySymbol)receiver).Type;
        if (receiverType is null || !IsCollection(receiverType))
        {
            return;
        }

        if (!state.IsWorkflowReachable(invocation, context.SemanticModel))
        {
            return;
        }

        var display = receiver.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.StaticCollectionMutation, invocation.GetLocation(), display));
    }

    private static bool IsCollection(ITypeSymbol type) =>
        TypeNames.IsOrImplements(type, "System.Collections.ICollection") ||
        TypeNames.IsOrImplements(type, "System.Collections.Generic.ICollection");

    private static void AnalyzeStaticObjectMutation(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (!MutatingVerbs.Contains(memberAccess.Name.Identifier.ValueText))
        {
            return;
        }

        var receiver = context.SemanticModel.GetSymbolInfo(memberAccess.Expression).Symbol;
        if (receiver is not IFieldSymbol { IsStatic: true } and not IPropertySymbol { IsStatic: true })
        {
            return;
        }

        if (IsSdkManagedStatic(receiver))
        {
            return;
        }

        var receiverType = (receiver as IFieldSymbol)?.Type ?? ((IPropertySymbol)receiver).Type;
        if (receiverType is null || !IsMutableReference(receiverType))
        {
            return;
        }

        if (!state.IsWorkflowReachable(invocation, context.SemanticModel))
        {
            return;
        }

        var display = receiver.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.StaticMethodMutation, invocation.GetLocation(), display));
    }

    private static bool IsMutableReference(ITypeSymbol type) =>
        type.IsReferenceType &&
        type.SpecialType != SpecialType.System_String &&
        !IsCollection(type);

    private static bool IsSdkManagedStatic(ISymbol symbol) =>
        symbol.ContainingType?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) == SdkNames.WorkflowType &&
        symbol.Name is "Signals" or "Updates";

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
