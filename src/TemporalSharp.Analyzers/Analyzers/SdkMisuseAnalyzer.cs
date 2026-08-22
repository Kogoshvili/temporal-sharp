using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using TemporalSharp.Analyzers.Analysis;
using TemporalSharp.Analyzers.Diagnostics;

namespace TemporalSharp.Analyzers.Analyzers;

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
            DiagnosticDescriptors.MissingStartToCloseTimeout,
            DiagnosticDescriptors.WaitConditionWithoutTimeout,
            DiagnosticDescriptors.WaitConditionTimeoutIgnored,
            DiagnosticDescriptors.NonSerializableType,
            DiagnosticDescriptors.SensitiveArgument,
            DiagnosticDescriptors.LossyNumber);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var state = CompilationAnalysisState.Get(startContext.Compilation, startContext.Options);
            var config = TemporalSharpConfig.From(startContext.Options.AnalyzerConfigOptionsProvider);

            startContext.RegisterSyntaxNodeAction(
                AnalyzeObjectCreation,
                SyntaxKind.ObjectCreationExpression);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeInvocation(c, state),
                SyntaxKind.InvocationExpression);

            startContext.RegisterSymbolAction(
                c => AnalyzeMethodSignature(c, config),
                SymbolKind.Method);
        });
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;
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

        // TMP2101 — no required timeout at all.
        if (!hasStartToClose && !hasScheduleToClose)
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.ActivityMissingTimeout, creation.GetLocation()));
        }

        // TMP2102 (opt-in) — ScheduleToCloseTimeout without StartToCloseTimeout.
        if (hasScheduleToClose && !hasStartToClose)
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.MissingStartToCloseTimeout, creation.GetLocation()));
        }
    }

    private static void AnalyzeMethodSignature(SymbolAnalysisContext context, TemporalSharpConfig config)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (!WorkflowDetection.IsActivityMethod(method) && !WorkflowDetection.IsWorkflowRunMethod(method))
        {
            return;
        }

        var payloadType = PayloadType(method);

        if (payloadType is not null && IsNonSerializable(payloadType))
        {
            ReportNonSerializable(context, payloadType, method.Locations[0]);
        }

        foreach (var parameter in method.Parameters)
        {
            if (IsNonSerializable(parameter.Type))
            {
                ReportNonSerializable(context, parameter.Type, parameter.Locations.Length > 0 ? parameter.Locations[0] : method.Locations[0]);
                continue;
            }

            if (IsLossyNumber(parameter.Type))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.LossyNumber,
                    parameter.Locations.Length > 0 ? parameter.Locations[0] : method.Locations[0],
                    parameter.Name,
                    parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                continue;
            }

            if (MatchesSensitivePattern(parameter.Name, method.DeclaringSyntaxReferences, config))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.SensitiveArgument,
                    parameter.Locations.Length > 0 ? parameter.Locations[0] : method.Locations[0],
                    parameter.Name));
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
        TemporalSharpConfig config)
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
            AnalyzeWorkflowApiInvocation(context, invocation, method);
        }

        AnalyzeLogging(context, state, invocation, method);
    }

    private static void AnalyzeWorkflowApiInvocation(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method)
    {
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
