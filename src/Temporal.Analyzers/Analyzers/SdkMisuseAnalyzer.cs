using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Kogoshvili.Temporal.Analyzers.Analysis;
using Kogoshvili.Temporal.Analyzers.Diagnostics;

namespace Kogoshvili.Temporal.Analyzers.Analyzers;

/// <summary>
/// Flags Temporal SDK feature-misuse: missing activity timeouts, string-named
/// workflow targets, discarded continue-as-new exceptions, and non-replay-aware
/// logging.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SdkMisuseAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> StringTargetMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ExecuteActivityAsync",
        "ExecuteLocalActivityAsync",
        "ExecuteChildWorkflowAsync",
        "StartChildWorkflowAsync",
        "CreateContinueAsNewException",
        "SignalWithStartWorkflowAsync");

    private static readonly ImmutableHashSet<string> LoggingMembers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "System.Console.WriteLine",
        "System.Console.Write",
        "System.IO.TextWriter.WriteLine",
        "System.IO.TextWriter.Write",
        "System.Diagnostics.Debug.WriteLine",
        "System.Diagnostics.Debug.Write",
        "System.Diagnostics.Debug.WriteLineIf",
        "System.Diagnostics.Debug.WriteIf",
        "System.Diagnostics.Trace.WriteLine",
        "System.Diagnostics.Trace.Write",
        "System.Diagnostics.Trace.WriteLineIf",
        "System.Diagnostics.Trace.WriteIf",
        "System.Diagnostics.Trace.TraceError",
        "System.Diagnostics.Trace.TraceInformation",
        "System.Diagnostics.Trace.TraceWarning");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.ActivityMissingTimeout,
            DiagnosticDescriptors.StringTarget,
            DiagnosticDescriptors.ContinueAsNewNotThrown,
            DiagnosticDescriptors.NonReplayAwareLogger,
            DiagnosticDescriptors.WaitConditionWithoutTimeout,
            DiagnosticDescriptors.WaitConditionTimeoutIgnored,
            DiagnosticDescriptors.NonSerializableType,
            DiagnosticDescriptors.SensitiveArgument,
            DiagnosticDescriptors.LossyNumber,
            DiagnosticDescriptors.BigIntegerInPayload,
            DiagnosticDescriptors.ExceptionInPayload,
            DiagnosticDescriptors.LargeInlinePayload,
            DiagnosticDescriptors.NestedLossyNumber);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var state = CompilationAnalysisState.Get(startContext.Compilation, startContext.Options);
            var config = TemporalConfig.From(startContext.Options.AnalyzerConfigOptionsProvider);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeObjectCreation(c, state),
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ImplicitObjectCreationExpression);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeInvocation(c, state),
                SyntaxKind.InvocationExpression);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeLargeStringLiteral(c, state),
                SyntaxKind.StringLiteralExpression);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeLargeCollection(c, state),
                SyntaxKind.ArrayInitializerExpression,
                SyntaxKind.CollectionInitializerExpression);

            startContext.RegisterSymbolAction(
                c => AnalyzeMethodSignature(c, config),
                SymbolKind.Method);
        });
    }

    // TMP2144 — oversized inline string literal.
    private static void AnalyzeLargeStringLiteral(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var literal = (LiteralExpressionSyntax)context.Node;
        if (!state.IsWorkflowReachable(literal, context.SemanticModel))
        {
            return;
        }

        if (literal.Token.ValueText.Length <= 1024)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.LargeInlinePayload,
            literal.GetLocation()));
    }

    // TMP2144 — oversized inline collection/array initializer.
    private static void AnalyzeLargeCollection(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var initializer = (InitializerExpressionSyntax)context.Node;
        if (initializer.IsKind(SyntaxKind.ObjectInitializerExpression))
        {
            return;
        }

        if (!state.IsWorkflowReachable(initializer, context.SemanticModel))
        {
            return;
        }

        if (initializer.Expressions.Count <= 20)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.LargeInlinePayload,
            initializer.GetLocation()));
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        var type = context.SemanticModel.GetTypeInfo(creation).Type;
        if (type is null)
        {
            return;
        }

        var typeName = type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        if (typeName != SdkNames.ActivityOptionsType && typeName != SdkNames.LocalActivityOptionsType)
        {
            return;
        }

        if (!state.IsWorkflowReachable(creation, context.SemanticModel))
        {
            return;
        }

        var initializer = creation.Initializer;
        if (initializer is null || initializer.Expressions.Count == 0)
        {
            return;
        }

        var hasStartToClose = false;
        var hasScheduleToClose = false;
        foreach (var expression in initializer.Expressions)
        {
            if (expression is AssignmentExpressionSyntax { Left: IdentifierNameSyntax identifier })
            {
                hasStartToClose |= identifier.Identifier.ValueText == "StartToCloseTimeout";
                hasScheduleToClose |= identifier.Identifier.ValueText == "ScheduleToCloseTimeout";
            }
        }

        // TMP2101 — neither required timeout set.
        if (!hasStartToClose && !hasScheduleToClose)
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.ActivityMissingTimeout, creation.GetLocation()));
        }
    }

    private static void AnalyzeMethodSignature(SymbolAnalysisContext context, TemporalConfig config)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (!WorkflowDetection.IsActivityMethod(method) && !WorkflowDetection.IsWorkflowRunMethod(method))
        {
            return;
        }

        var payloadType = PayloadType(method);

        if (payloadType is not null)
        {
            if (IsNonSerializable(payloadType))
            {
                ReportNonSerializable(context, payloadType, method.Locations[0]);
            }
            else
            {
                ReportPayloadTypeIssue(context, payloadType, method.Locations[0]);
                ReportNestedLossyMembers(context, payloadType, new HashSet<ISymbol>(SymbolEqualityComparer.Default));
            }
        }

        foreach (var parameter in method.Parameters)
        {
            var location = parameter.Locations.Length > 0 ? parameter.Locations[0] : method.Locations[0];

            if (IsNonSerializable(parameter.Type))
            {
                ReportNonSerializable(context, parameter.Type, location);
                continue;
            }

            if (ReportPayloadTypeIssue(context, parameter.Type, location))
            {
                continue;
            }

            if (IsLossyNumber(parameter.Type))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.LossyNumber,
                    location,
                    parameter.Name,
                    parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                continue;
            }

            ReportNestedLossyMembers(context, parameter.Type, new HashSet<ISymbol>(SymbolEqualityComparer.Default));

            if (MatchesSensitivePattern(parameter.Name, method.DeclaringSyntaxReferences, config))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.SensitiveArgument,
                    location,
                    parameter.Name));
            }
        }
    }

    private static bool ReportPayloadTypeIssue(SymbolAnalysisContext context, ITypeSymbol type, Location location)
    {
        if (TypeNames.FullName(type) == "System.Numerics.BigInteger")
        {
            ReportType(context, DiagnosticDescriptors.BigIntegerInPayload, type, location);
            return true;
        }

        if (TypeNames.IsOrDerivesFrom(type, "System.Exception"))
        {
            ReportType(context, DiagnosticDescriptors.ExceptionInPayload, type, location);
            return true;
        }

        return false;
    }

    private static void ReportType(SymbolAnalysisContext context, DiagnosticDescriptor descriptor, ITypeSymbol type, Location location)
    {
        var display = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, display));
    }

    private static void ReportNestedLossyMembers(
        SymbolAnalysisContext context,
        ITypeSymbol type,
        HashSet<ISymbol> visited)
    {
        if (type is not INamedTypeSymbol named ||
            named.DeclaringSyntaxReferences.Length == 0 ||
            !visited.Add(named))
        {
            return;
        }

        foreach (var member in named.GetMembers())
        {
            ITypeSymbol? memberType = member switch
            {
                IPropertySymbol { DeclaredAccessibility: Accessibility.Public } property => property.Type,
                IFieldSymbol { DeclaredAccessibility: Accessibility.Public } field => field.Type,
                _ => null,
            };

            if (memberType is null)
            {
                continue;
            }

            if (IsLossyNumber(memberType))
            {
                var location = member.Locations.Length > 0 ? member.Locations[0] : context.Symbol.Locations[0];
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.NestedLossyNumber,
                    location,
                    member.Name,
                    memberType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }
            else
            {
                ReportNestedLossyMembers(context, memberType, visited);
            }
        }
    }

    private static ITypeSymbol? PayloadType(IMethodSymbol method)
    {
        if (method.ReturnType is INamedTypeSymbol named &&
            TypeNames.FullName(named) == "System.Threading.Tasks.Task" &&
            named.TypeArguments.Length == 1)
        {
            return named.TypeArguments[0];
        }

        return null;
    }

    private static bool IsNonSerializable(ITypeSymbol type) =>
        type.TypeKind == TypeKind.Delegate ||
        TypeNames.IsOrDerivesFrom(type, "System.IO.Stream") ||
        TypeNames.IsOrDerivesFrom(type, "System.Reflection.MemberInfo") ||
        TypeNames.FullName(type) == "System.Type" ||
        TypeNames.IsOrImplements(type, "System.Collections.Generic.IAsyncEnumerable") ||
        TypeNames.FullName(type) is "System.Threading.Channels.Channel" or
            "System.Threading.Channels.ChannelReader" or
            "System.Threading.Channels.ChannelWriter";

    private static bool IsLossyNumber(ITypeSymbol type) =>
        type.TypeKind == TypeKind.Dynamic ||
        type.SpecialType == SpecialType.System_Object ||
        TypeNames.FullName(type) == "System.Text.Json.JsonElement";

    private static void ReportNonSerializable(SymbolAnalysisContext context, ITypeSymbol type, Location location)
    {
        var display = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.NonSerializableType, location, display));
    }

    private static bool MatchesSensitivePattern(
        string name,
        ImmutableArray<SyntaxReference> declaringSyntaxReferences,
        TemporalConfig config)
    {
        SyntaxTree? tree = null;
        if (declaringSyntaxReferences.Length > 0)
        {
            tree = declaringSyntaxReferences[0].SyntaxTree;
        }

        var pattern = config.SensitivePattern(tree);
        return Regex.IsMatch(name, pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (method.ContainingType?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) == SdkNames.WorkflowType)
        {
            AnalyzeWorkflowApiInvocation(context, state, invocation, method);
        }

        AnalyzeLogging(context, state, invocation, method);
    }

    private static void AnalyzeWorkflowApiInvocation(
        SyntaxNodeAnalysisContext context,
        CompilationAnalysisState state,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method)
    {
        if (!state.IsWorkflowReachable(invocation, context.SemanticModel))
        {
            return;
        }

        if (StringTargetMethods.Contains(method.Name) &&
            method.Parameters.Length > 0 &&
            method.Parameters[0].Type.SpecialType == SpecialType.System_String)
        {
            var target = invocation.ArgumentList.Arguments.Count > 0
                ? invocation.ArgumentList.Arguments[0].ToString()
                : method.Name;

            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.StringTarget, invocation.GetLocation(), target));
        }

        if (method.Name == "CreateContinueAsNewException" && IsDiscarded(invocation))
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.ContinueAsNewNotThrown, invocation.GetLocation()));
        }

        if (method.Name == "WaitConditionAsync")
        {
            AnalyzeWaitCondition(context, invocation, method);
        }
    }

    private static void AnalyzeWaitCondition(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method)
    {
        if (method.ReturnType is not INamedTypeSymbol returnType ||
            TypeNames.FullName(returnType) != "System.Threading.Tasks.Task")
        {
            return;
        }

        if (returnType.TypeArguments.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.WaitConditionWithoutTimeout,
                invocation.GetLocation()));
            return;
        }

        if (IsAwaitedResultDiscarded(invocation))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.WaitConditionTimeoutIgnored,
                invocation.GetLocation()));
        }
    }

    private static void AnalyzeLogging(
        SyntaxNodeAnalysisContext context,
        CompilationAnalysisState state,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method)
    {
        if (!LoggingMembers.Contains(SymbolKeys.Member(method)))
        {
            return;
        }

        if (!state.IsWorkflowReachable(invocation, context.SemanticModel))
        {
            return;
        }

        var display = method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.NonReplayAwareLogger, invocation.GetLocation(), display));
    }

    private static bool IsDiscarded(ExpressionSyntax expression) =>
        expression.Parent is ExpressionStatementSyntax ||
        expression.Parent is AssignmentExpressionSyntax { Left: IdentifierNameSyntax { Identifier.ValueText: "_" } };

    private static bool IsAwaitedResultDiscarded(InvocationExpressionSyntax invocation) =>
        invocation.Parent is AwaitExpressionSyntax awaitExpression
            ? IsDiscarded(awaitExpression)
            : IsDiscarded(invocation);
}
