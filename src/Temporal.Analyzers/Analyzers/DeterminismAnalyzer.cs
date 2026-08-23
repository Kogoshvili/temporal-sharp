using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Kogoshvili.Temporal.Analyzers.Analysis;
using Kogoshvili.Temporal.Analyzers.Diagnostics;

namespace Kogoshvili.Temporal.Analyzers.Analyzers;

/// <summary>
/// Flags non-deterministic member access (wall-clock time, sleep/block,
/// randomness, I/O) in code reachable from workflow code.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DeterminismAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableArray<DiagnosticDescriptor> Supported =
        ImmutableArray.Create(
            DiagnosticDescriptors.WallClockTime,
            DiagnosticDescriptors.BlockOrSleep,
            DiagnosticDescriptors.ConfigureAwaitFalse,
            DiagnosticDescriptors.FloatingTask,
            DiagnosticDescriptors.NonDeterministicRandomness,
            DiagnosticDescriptors.StopwatchUsage,
            DiagnosticDescriptors.IoOrEnvironmentAccess,
            DiagnosticDescriptors.ConcurrentExecution,
            DiagnosticDescriptors.ConcurrentTaskRun,
            DiagnosticDescriptors.BlockingPrimitive,
            DiagnosticDescriptors.BlockingSyncReplacement,
            DiagnosticDescriptors.TaskScheduling,
            DiagnosticDescriptors.ManualTaskCoordination,
            DiagnosticDescriptors.ReflectionInvocation,
            DiagnosticDescriptors.AmbientState,
            DiagnosticDescriptors.UnorderedEnumeration,
            DiagnosticDescriptors.CultureSensitiveParse,
            DiagnosticDescriptors.CryptoRandomness,
            DiagnosticDescriptors.Finalizer,
            DiagnosticDescriptors.TimerScheduling,
            DiagnosticDescriptors.WeakReference,
            DiagnosticDescriptors.ModuleSideEffect,
            DiagnosticDescriptors.NondeterministicControlFlow,
            DiagnosticDescriptors.WallClockComparison,
            DiagnosticDescriptors.PersistedIdRandomness);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Supported;

    private static readonly ImmutableHashSet<string> OrderExposingLinqMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ToList", "ToArray",
        "First", "FirstOrDefault", "Last", "LastOrDefault",
        "Single", "SingleOrDefault",
        "ElementAt", "ElementAtOrDefault",
        "Take", "TakeWhile", "Skip", "SkipWhile");

    private static readonly ImmutableHashSet<string> TransparentLinqMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Where", "Select", "SelectMany", "OfType", "Cast", "AsEnumerable",
        "Distinct", "DefaultIfEmpty", "Append", "Prepend", "Concat");

    private static readonly ImmutableHashSet<string> CultureSensitiveParseTypes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "System.Int16", "System.Int32", "System.Int64",
        "System.UInt16", "System.UInt32", "System.UInt64",
        "System.Byte", "System.SByte",
        "System.Single", "System.Double", "System.Decimal",
        "System.DateTime", "System.DateTimeOffset", "System.TimeSpan");

    // Types whose parameterless ToString() has a culture-dependent representation.
    // Integral types format only digits and a sign, and TimeSpan.ToString() is the
    // invariant "c" format, so they are intentionally excluded.
    private static readonly ImmutableHashSet<string> CultureSensitiveDefaultToStringTypes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "System.Single", "System.Double", "System.Decimal",
        "System.DateTime", "System.DateTimeOffset");

    private static readonly ImmutableHashSet<string> ParseMethodNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Parse", "ParseExact", "TryParse", "TryParseExact");

    private static readonly SyntaxKind[] ComparisonKinds =
    {
        SyntaxKind.EqualsExpression,
        SyntaxKind.NotEqualsExpression,
        SyntaxKind.LessThanExpression,
        SyntaxKind.LessThanOrEqualExpression,
        SyntaxKind.GreaterThanExpression,
        SyntaxKind.GreaterThanOrEqualExpression,
    };

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var state = CompilationAnalysisState.Get(startContext.Compilation, startContext.Options);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, state),
                SyntaxKind.InvocationExpression);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeUnorderedMaterialization(nodeContext, state),
                SyntaxKind.InvocationExpression);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeObjectCreation(nodeContext, state),
                SyntaxKind.ObjectCreationExpression);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeMemberAccess(nodeContext, state),
                SyntaxKind.SimpleMemberAccessExpression);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeForEach(nodeContext, state),
                SyntaxKind.ForEachStatement);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeLock(nodeContext, state),
                SyntaxKind.LockStatement);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeCultureSensitiveCall(nodeContext, state),
                SyntaxKind.InvocationExpression);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeFinalizer(nodeContext, state),
                SyntaxKind.DestructorDeclaration);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeStaticConstructor(nodeContext, state),
                SyntaxKind.ConstructorDeclaration);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeStaticFieldInitializer(nodeContext, state),
                SyntaxKind.VariableDeclarator);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeModuleInitializer(nodeContext, state),
                SyntaxKind.MethodDeclaration);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeWallClockComparison(nodeContext, state),
                ComparisonKinds);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeControlFlow(nodeContext, state),
                SyntaxKind.IfStatement,
                SyntaxKind.WhileStatement,
                SyntaxKind.ForStatement,
                SyntaxKind.DoStatement,
                SyntaxKind.SwitchStatement,
                SyntaxKind.ConditionalExpression);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzePersistedId(nodeContext, state),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var node = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(node).Symbol is not IMethodSymbol symbol)
        {
            return;
        }

        // TMP0113 — ConfigureAwait(false) leaves the workflow synchronization context.
        if (IsConfigureAwaitFalse(node, symbol))
        {
            ReportIfReachable(context, state, node, symbol, DiagnosticDescriptors.ConfigureAwaitFalse);
            return;
        }

        // TMP0147 — Mutex / Semaphore / SemaphoreSlim have deterministic replacements.
        if (TryGetBlockingSyncReplacementDescriptor(node, symbol, context.SemanticModel) is { } replacement)
        {
            ReportIfReachable(context, state, node, symbol, replacement);
            return;
        }

        if (DenyList.TryGetMember(SymbolKeys.Member(symbol), out var descriptor))
        {
            ReportIfReachable(context, state, node, symbol, descriptor);
            return;
        }

        // TMP0112 — an un-awaited (floating) task-returning call, reported only for
        // calls that are not already handled by a deny-list rule above.
        if (node.Parent is ExpressionStatementSyntax && IsTaskLike(symbol.ReturnType))
        {
            ReportIfReachable(context, state, node, symbol, DiagnosticDescriptors.FloatingTask);
        }
    }

    private static bool IsTaskLike(ITypeSymbol type)
    {
        var name = TypeNames.FullName(type);
        return name is "System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask";
    }

    private static bool IsConfigureAwaitFalse(InvocationExpressionSyntax node, IMethodSymbol symbol)
    {
        if (symbol.Name != "ConfigureAwait")
        {
            return false;
        }

        var typeName = TypeNames.FullName(symbol.ContainingType);
        if (typeName is not ("System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask"))
        {
            return false;
        }

        var argument = node.ArgumentList?.Arguments.FirstOrDefault();
        return argument?.Expression is LiteralExpressionSyntax literal &&
               literal.IsKind(SyntaxKind.FalseLiteralExpression);
    }

    private static DiagnosticDescriptor? TryGetBlockingSyncReplacementDescriptor(
        InvocationExpressionSyntax node,
        IMethodSymbol symbol,
        SemanticModel model)
    {
        if (symbol.Name is not ("WaitOne" or "Wait" or "WaitAsync"))
        {
            return null;
        }

        if (node.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return null;
        }

        var receiverType = model.GetTypeInfo(memberAccess.Expression).Type;
        if (receiverType is null)
        {
            return null;
        }

        return TypeNames.FullName(receiverType) switch
        {
            "System.Threading.Mutex" or
            "System.Threading.Semaphore" or
            "System.Threading.SemaphoreSlim" => DiagnosticDescriptors.BlockingSyncReplacement,
            _ => null,
        };
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var node = (ObjectCreationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(node).Symbol is not IMethodSymbol symbol)
        {
            return;
        }

        var key = SymbolKeys.Member(symbol);

        // Concurrency constructors (e.g. new Thread(...), new BackgroundWorker())
        // are flagged regardless of argument count.
        if (DenyList.TryGetAnyArgConstructor(key, out var concurrencyDescriptor))
        {
            ReportIfReachable(context, state, node, symbol, concurrencyDescriptor);
            return;
        }

        // Only parameterless constructors of non-deterministic types are flagged
        // (e.g. new Random()); a seeded constructor is deterministic.
        if (node.ArgumentList is null || node.ArgumentList.Arguments.Count != 0)
        {
            return;
        }

        if (!DenyList.TryGetConstructor(key, out var descriptor))
        {
            return;
        }

        ReportIfReachable(context, state, node, symbol, descriptor);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var node = (MemberAccessExpressionSyntax)context.Node;
        var symbol = context.SemanticModel.GetSymbolInfo(node).Symbol;

        // Only property/field reads; method groups are handled by invocation
        // analysis and must not be double-reported.
        if (symbol is not (IPropertySymbol or IFieldSymbol))
        {
            return;
        }

        if (!DenyList.TryGetMember(SymbolKeys.Member(symbol), out var descriptor))
        {
            return;
        }

        ReportIfReachable(context, state, node, symbol, descriptor);
    }

    private static void AnalyzeForEach(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var node = (ForEachStatementSyntax)context.Node;
        if (!state.IsWorkflowReachable(node, context.SemanticModel))
        {
            return;
        }

        if (!TryGetUnorderedSource(node.Expression, context.SemanticModel, out var collectionType))
        {
            return;
        }

        var display = collectionType!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UnorderedEnumeration, node.ForEachKeyword.GetLocation(), display));
    }

    private static void AnalyzeUnorderedMaterialization(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var node = (InvocationExpressionSyntax)context.Node;
        if (!state.IsWorkflowReachable(node, context.SemanticModel))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(node).Symbol is not IMethodSymbol method ||
            !IsLinqMethod(method) ||
            !OrderExposingLinqMethods.Contains(method.Name))
        {
            return;
        }

        var source = SourceExpression(node, method);
        if (source is null || !TryGetUnorderedSource(source, context.SemanticModel, out var type))
        {
            return;
        }

        var display = type!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UnorderedEnumeration, node.GetLocation(), display));
    }

    private static bool IsLinqMethod(IMethodSymbol method) =>
        method.ContainingType is { } containingType &&
        (TypeNames.FullName(containingType) == "System.Linq.Enumerable" ||
         TypeNames.FullName(containingType) == "System.Linq.Queryable");

    private static ExpressionSyntax? SourceExpression(InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        if (method.MethodKind == MethodKind.ReducedExtension)
        {
            return invocation.Expression is MemberAccessExpressionSyntax memberAccess
                ? memberAccess.Expression
                : null;
        }

        return invocation.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
    }

    /// <summary>
    /// Determines whether <paramref name="expression"/> enumerates an unordered
    /// collection. Walks through order-preserving LINQ operators and the
    /// Dictionary.Keys/Values views; OrderBy/OrderByDescending and sorted
    /// collection types terminate the walk as deterministic.
    /// </summary>
    private static bool TryGetUnorderedSource(ExpressionSyntax expression, SemanticModel model, out ITypeSymbol? type)
    {
        if (UnorderedCollections.IsOrderBy(expression))
        {
            type = null;
            return false;
        }

        if (expression is InvocationExpressionSyntax invocation &&
            model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method &&
            IsLinqMethod(method) &&
            TransparentLinqMethods.Contains(method.Name))
        {
            var inner = SourceExpression(invocation, method);
            if (inner is not null)
            {
                return TryGetUnorderedSource(inner, model, out type);
            }
        }

        if (expression is MemberAccessExpressionSyntax member &&
            member.Name.Identifier.ValueText is "Keys" or "Values")
        {
            var receiverType = model.GetTypeInfo(member.Expression).Type;
            if (receiverType is not null && UnorderedCollections.IsUnordered(receiverType))
            {
                type = model.GetTypeInfo(expression).Type;
                return type is not null;
            }
        }

        var collectionType = model.GetTypeInfo(expression).Type;
        if (collectionType is not null && UnorderedCollections.IsUnordered(collectionType))
        {
            type = collectionType;
            return true;
        }

        type = null;
        return false;
    }

    private static void AnalyzeLock(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var node = (LockStatementSyntax)context.Node;
        if (!state.IsWorkflowReachable(node, context.SemanticModel))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.BlockingPrimitive, node.LockKeyword.GetLocation(), "lock"));
    }

    private static void AnalyzeCultureSensitiveCall(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var node = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(node).Symbol is not IMethodSymbol symbol)
        {
            return;
        }

        if (!IsCultureSensitiveWithoutProvider(symbol))
        {
            return;
        }

        ReportIfReachable(context, state, node, symbol, DiagnosticDescriptors.CultureSensitiveParse);
    }

    private static bool IsCultureSensitiveWithoutProvider(IMethodSymbol symbol)
    {
        var typeName = TypeNames.FullName(symbol.ContainingType);

        if (symbol.Name == "Format")
        {
            return typeName == "System.String" && !HasProviderParameter(symbol);
        }

        if (HasProviderParameter(symbol))
        {
            return false;
        }

        if (symbol.Name == "ToString")
        {
            if (!CultureSensitiveParseTypes.Contains(typeName))
            {
                return false;
            }

            // Parameterless ToString() is only culture-sensitive for types whose
            // default representation varies by culture (floating-point, dates).
            if (symbol.Parameters.IsEmpty && !CultureSensitiveDefaultToStringTypes.Contains(typeName))
            {
                return false;
            }

            return true;
        }

        if (ParseMethodNames.Contains(symbol.Name))
        {
            return CultureSensitiveParseTypes.Contains(typeName);
        }

        return false;
    }

    private static bool HasProviderParameter(IMethodSymbol symbol)
        => symbol.Parameters.Any(p => TypeNames.FullName(p.Type) == "System.IFormatProvider");

    // TMP0171 — finalizer on a [Workflow] type (GC timing is non-deterministic).
    private static void AnalyzeFinalizer(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var node = (DestructorDeclarationSyntax)context.Node;
        var type = context.SemanticModel.GetDeclaredSymbol(node)?.ContainingType;
        if (type is null || !WorkflowDetection.IsWorkflowType(type))
        {
            return;
        }

        var display = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.Finalizer, node.Identifier.GetLocation(), display));
    }

    // TMP0177 — static constructor that schedules workflow commands.
    private static void AnalyzeStaticConstructor(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var node = (ConstructorDeclarationSyntax)context.Node;
        if (!node.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            return;
        }

        var type = context.SemanticModel.GetDeclaredSymbol(node)?.ContainingType;
        if (type is null || !WorkflowDetection.IsWorkflowType(type))
        {
            return;
        }

        if (!ContainsWorkflowCommand(node, context.SemanticModel))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ModuleSideEffect,
            node.Identifier.GetLocation(),
            "static constructor"));
    }

    // TMP0177 — static field initializer that schedules workflow commands.
    private static void AnalyzeStaticFieldInitializer(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var declarator = (VariableDeclaratorSyntax)context.Node;
        if (declarator.Initializer is null ||
            declarator.Parent?.Parent is not FieldDeclarationSyntax field ||
            !field.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            return;
        }

        var fieldSymbol = context.SemanticModel.GetDeclaredSymbol(declarator) as IFieldSymbol;
        var type = fieldSymbol?.ContainingType;
        if (type is null || !WorkflowDetection.IsWorkflowType(type))
        {
            return;
        }

        if (!ContainsWorkflowCommand(declarator.Initializer.Value, context.SemanticModel))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ModuleSideEffect,
            declarator.GetLocation(),
            "static field initializer"));
    }

    // TMP0177 — [ModuleInitializer] method that schedules workflow commands.
    private static void AnalyzeModuleInitializer(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var node = (MethodDeclarationSyntax)context.Node;
        var method = context.SemanticModel.GetDeclaredSymbol(node);
        if (method is null || !HasModuleInitializerAttribute(method))
        {
            return;
        }

        if (!ContainsWorkflowCommand(node, context.SemanticModel))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ModuleSideEffect,
            node.Identifier.GetLocation(),
            "module initializer"));
    }

    private static bool HasModuleInitializerAttribute(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ==
                "System.Runtime.CompilerServices.ModuleInitializerAttribute")
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsWorkflowCommand(SyntaxNode node, SemanticModel model)
    {
        foreach (var descendant in node.DescendantNodesAndSelf())
        {
            if (descendant is InvocationExpressionSyntax invocation &&
                model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method &&
                SdkNames.IsWorkflowCommand(method))
            {
                return true;
            }
        }

        return false;
    }

    // TMP0104 — Workflow.UtcNow compared to a persisted (non-UtcNow) value.
    private static void AnalyzeWallClockComparison(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var node = (BinaryExpressionSyntax)context.Node;
        if (!state.IsWorkflowReachable(node, context.SemanticModel))
        {
            return;
        }

        var leftIsClock = IsWorkflowUtcNow(node.Left, context.SemanticModel);
        var rightIsClock = IsWorkflowUtcNow(node.Right, context.SemanticModel);
        if (leftIsClock == rightIsClock)
        {
            return;
        }

        var clockOperand = leftIsClock ? node.Left : node.Right;
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.WallClockComparison,
            clockOperand.GetLocation(),
            clockOperand.ToString()));
    }

    private static bool IsWorkflowUtcNow(ExpressionSyntax expression, SemanticModel model)
    {
        if (expression is MemberAccessExpressionSyntax memberAccess &&
            model.GetSymbolInfo(memberAccess).Symbol is IPropertySymbol property &&
            property.Name == "UtcNow" &&
            property.ContainingType is not null &&
            SdkNames.IsWorkflowType(property.ContainingType))
        {
            return true;
        }

        return expression is InvocationExpressionSyntax invocation &&
               model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method &&
               method.Name == "UtcNow" &&
               method.ContainingType is not null &&
               SdkNames.IsWorkflowType(method.ContainingType);
    }

    // TMP0175 — control flow that branches/loops on non-deterministic time or randomness.
    private static void AnalyzeControlFlow(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var node = context.Node;
        if (!state.IsWorkflowReachable(node, context.SemanticModel))
        {
            return;
        }

        var condition = node switch
        {
            IfStatementSyntax ifStatement => ifStatement.Condition,
            WhileStatementSyntax whileStatement => whileStatement.Condition,
            ForStatementSyntax forStatement => forStatement.Condition,
            DoStatementSyntax doStatement => doStatement.Condition,
            SwitchStatementSyntax switchStatement => switchStatement.Expression,
            ConditionalExpressionSyntax conditional => conditional.Condition,
            _ => null,
        };

        if (condition is null || !ContainsNondeterministicSource(condition, context.SemanticModel))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.NondeterministicControlFlow,
            condition.GetLocation(),
            "control flow"));
    }

    private static bool ContainsNondeterministicSource(ExpressionSyntax expression, SemanticModel model)
    {
        foreach (var node in expression.DescendantNodesAndSelf())
        {
            ISymbol? symbol = node switch
            {
                InvocationExpressionSyntax invocation => model.GetSymbolInfo(invocation).Symbol,
                MemberAccessExpressionSyntax memberAccess => model.GetSymbolInfo(memberAccess).Symbol,
                ObjectCreationExpressionSyntax creation => model.GetSymbolInfo(creation).Symbol,
                _ => null,
            };

            if (symbol is not null && IsNondeterministicSource(symbol))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNondeterministicSource(ISymbol symbol)
    {
        var key = SymbolKeys.Member(symbol);
        DiagnosticDescriptor? descriptor = null;
        if (DenyList.TryGetMember(key, out var memberDescriptor))
        {
            descriptor = memberDescriptor;
        }
        else if (DenyList.TryGetConstructor(key, out var constructorDescriptor))
        {
            descriptor = constructorDescriptor;
        }
        else if (DenyList.TryGetAnyArgConstructor(key, out var anyArgDescriptor))
        {
            descriptor = anyArgDescriptor;
        }

        return descriptor == DiagnosticDescriptors.WallClockTime ||
               descriptor == DiagnosticDescriptors.NonDeterministicRandomness ||
               descriptor == DiagnosticDescriptors.StopwatchUsage ||
               descriptor == DiagnosticDescriptors.CryptoRandomness;
    }

    // TMP0123 — Workflow.Random / Workflow.NewGuid passed into a workflow command
    // (i.e. leaving the workflow as a persisted id or payload).
    private static void AnalyzePersistedId(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
            !SdkNames.IsWorkflowCommand(method))
        {
            return;
        }

        if (!state.IsWorkflowReachable(invocation, context.SemanticModel))
        {
            return;
        }

        if (invocation.ArgumentList is not { } argumentList)
        {
            return;
        }

        foreach (var argument in argumentList.Arguments)
        {
            if (FindWorkflowRandomness(argument.Expression, context.SemanticModel) is not { } randomness)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.PersistedIdRandomness,
                randomness.GetLocation(),
                randomness.ToString()));
            return;
        }
    }

    private static ExpressionSyntax? FindWorkflowRandomness(ExpressionSyntax expression, SemanticModel model)
    {
        foreach (var node in expression.DescendantNodesAndSelf())
        {
            if (node is InvocationExpressionSyntax invocation &&
                model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method)
            {
                if (SdkNames.IsWorkflowType(method.ContainingType) && method.Name == "NewGuid")
                {
                    return invocation;
                }

                if (TypeNames.FullName(method.ContainingType) == "System.Random" &&
                    invocation.Expression is MemberAccessExpressionSyntax receiverAccess &&
                    IsWorkflowRandomProperty(receiverAccess.Expression, model))
                {
                    return invocation;
                }
            }
            else if (node is MemberAccessExpressionSyntax access &&
                     model.GetSymbolInfo(access).Symbol is IPropertySymbol or IFieldSymbol &&
                     IsWorkflowRandomProperty(access, model))
            {
                return access;
            }
        }

        return null;
    }

    private static bool IsWorkflowRandomProperty(ExpressionSyntax expression, SemanticModel model)
    {
        if (expression is not MemberAccessExpressionSyntax access)
        {
            return false;
        }

        var symbol = model.GetSymbolInfo(access).Symbol;
        if (symbol is null || (symbol is not IPropertySymbol && symbol is not IFieldSymbol))
        {
            return false;
        }

        return symbol.Name == "Random" &&
               symbol.ContainingType is not null &&
               SdkNames.IsWorkflowType(symbol.ContainingType);
    }

    private static void ReportIfReachable(
        SyntaxNodeAnalysisContext context,
        CompilationAnalysisState state,
        SyntaxNode node,
        ISymbol symbol,
        DiagnosticDescriptor? descriptor)
    {
        if (descriptor is null)
        {
            return;
        }

        if (!state.IsWorkflowReachable(node, context.SemanticModel))
        {
            return;
        }

        var display = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        context.ReportDiagnostic(Diagnostic.Create(descriptor, node.GetLocation(), display));
    }
}
