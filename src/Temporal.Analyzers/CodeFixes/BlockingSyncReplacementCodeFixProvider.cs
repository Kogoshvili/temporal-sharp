using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Kogoshvili.Temporal.Analyzers.Analysis;

namespace Kogoshvili.Temporal.Analyzers.CodeFixes;

/// <summary>
/// Rewrites a blocking synchronization primitive (TMP0147) into its
/// deterministic SDK replacement: <c>System.Threading.Mutex</c> →
/// <c>Temporalio.Workflows.Mutex</c>, and <c>System.Threading.Semaphore</c> /
/// <c>SemaphoreSlim</c> → <c>Temporalio.Workflows.Semaphore</c>. Because the SDK
/// waits are async, the fix both renames the wait call into its
/// <c>*Async</c> form (and awaits it) and rewrites the receiver's declaration
/// type and constructor.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BlockingSyncReplacementCodeFixProvider)), Shared]
public sealed class BlockingSyncReplacementCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("TMP0147");

    public override FixAllProvider? GetFixAllProvider() => null;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics.FirstOrDefault();
        if (diagnostic is null)
        {
            return;
        }

        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        if (node is not InvocationExpressionSyntax invocation ||
            invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
            invocation.Parent is not ExpressionStatementSyntax)
        {
            return;
        }

        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (model is null)
        {
            return;
        }

        var receiverType = model.GetTypeInfo(memberAccess.Expression).Type;
        if (receiverType is null ||
            MapTarget(receiverType, memberAccess.Name.Identifier.ValueText) is not { } mapping)
        {
            return;
        }

        var replacements = BuildReplacements(model, invocation, memberAccess, receiverType, mapping);
        if (replacements is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Use {mapping.TargetType.Split('.').Last()}",
                ct => ApplyAsync(context.Document, replacements, ct),
                equivalenceKey: mapping.Method),
            diagnostic);
    }

    private sealed class Mapping
    {
        public Mapping(string targetType, string method)
        {
            TargetType = targetType;
            Method = method;
        }

        public string TargetType { get; }

        public string Method { get; }
    }

    private static Mapping? MapTarget(ITypeSymbol receiverType, string methodName)
    {
        switch (TypeNames.FullName(receiverType))
        {
            case "System.Threading.Mutex" when methodName == "WaitOne":
                return new Mapping("Temporalio.Workflows.Mutex", "WaitOneAsync");

            case "System.Threading.Semaphore" when methodName == "WaitOne":
                return new Mapping("Temporalio.Workflows.Semaphore", "WaitAsync");

            case "System.Threading.SemaphoreSlim" when methodName is "Wait" or "WaitAsync":
                return new Mapping("Temporalio.Workflows.Semaphore", "WaitAsync");

            default:
                return null;
        }
    }

    private static Dictionary<SyntaxNode, SyntaxNode>? BuildReplacements(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess,
        ITypeSymbol receiverType,
        Mapping mapping)
    {
        var declarator = GetDeclarator(model, memberAccess.Expression);
        if (declarator is null)
        {
            return null;
        }

        var declaration = declarator.Parent as VariableDeclarationSyntax;
        if (declaration is null)
        {
            return null;
        }

        var replacements = new Dictionary<SyntaxNode, SyntaxNode>();
        var newType = SyntaxFactory.ParseTypeName(mapping.TargetType);
        if (!IsImplicitlyTyped(declaration))
        {
            replacements[declaration.Type] = newType.WithTriviaFrom(declaration.Type);
        }

        if (declarator.Initializer is { } initializer)
        {
            if (initializer.Value is ObjectCreationExpressionSyntax creation)
            {
                if (!TryRewriteCreation(creation, TypeNames.FullName(receiverType), mapping.TargetType, out var newCreation))
                {
                    return null;
                }

                if (newCreation is not null)
                {
                    replacements[creation] = newCreation;
                }
            }
            else if (initializer.Value is ImplicitObjectCreationExpressionSyntax implicitCreation)
            {
                if (!ValidateSemaphoreArgs(implicitCreation.ArgumentList, TypeNames.FullName(receiverType)))
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        // Call site: rename the wait method and await it.
        var newMemberAccess = memberAccess.WithName(SyntaxFactory.IdentifierName(mapping.Method));
        var awaited = SyntaxFactory.AwaitExpression(
            SyntaxFactory.Token(SyntaxKind.AwaitKeyword).WithTrailingTrivia(SyntaxFactory.Space),
            invocation.WithExpression(newMemberAccess));
        replacements[invocation] = awaited;

        // Ensure the enclosing method is async.
        var function = CodeFixHelpers.EnclosingFunction(invocation);
        var method = CodeFixHelpers.EnclosingMethod(model, invocation);
        if (function is not null && method is { IsAsync: false } && CodeFixHelpers.IsAsyncCompatibleReturn(method.ReturnType))
        {
            replacements[function] = CodeFixHelpers.AddAsyncModifier(function);
        }

        return replacements;
    }

    private static bool IsImplicitlyTyped(VariableDeclarationSyntax declaration) =>
        declaration.Type is IdentifierNameSyntax { Identifier.ValueText: "var" };

    private static VariableDeclaratorSyntax? GetDeclarator(SemanticModel model, ExpressionSyntax receiver)
    {
        var symbol = model.GetSymbolInfo(receiver).Symbol;
        if (symbol is not (ILocalSymbol or IFieldSymbol))
        {
            return null;
        }

        var reference = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        return reference?.GetSyntax() as VariableDeclaratorSyntax;
    }

    private static bool TryRewriteCreation(
        ObjectCreationExpressionSyntax creation,
        string receiverFullName,
        string targetType,
        out ObjectCreationExpressionSyntax? newCreation)
    {
        newCreation = null;

        if (receiverFullName == "System.Threading.Mutex")
        {
            if (creation.ArgumentList is null || creation.ArgumentList.Arguments.Count != 0)
            {
                return false;
            }

            newCreation = creation.WithType(SyntaxFactory.ParseTypeName(targetType).WithTriviaFrom(creation.Type));
            return true;
        }

        if (creation.ArgumentList is not { } argumentList ||
            !TryRewriteSemaphoreArgs(argumentList, receiverFullName, out var newArgs))
        {
            return false;
        }

        newCreation = creation
            .WithType(SyntaxFactory.ParseTypeName(targetType).WithTriviaFrom(creation.Type))
            .WithArgumentList(newArgs);
        return true;
    }

    private static bool ValidateSemaphoreArgs(ArgumentListSyntax args, string receiverFullName)
    {
        if (receiverFullName == "System.Threading.Mutex")
        {
            return args.Arguments.Count == 0;
        }

        return TryRewriteSemaphoreArgs(args, receiverFullName, out _);
    }

    private static bool TryRewriteSemaphoreArgs(
        ArgumentListSyntax args,
        string receiverFullName,
        out ArgumentListSyntax newArgs)
    {
        newArgs = args;
        var arguments = args.Arguments;

        if (receiverFullName == "System.Threading.SemaphoreSlim" && arguments.Count == 1)
        {
            return true;
        }

        if (arguments.Count == 2 &&
            TryGetIntLiteral(arguments[0].Expression) is int initial &&
            TryGetIntLiteral(arguments[1].Expression) is int max &&
            initial == max)
        {
            newArgs = SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(arguments[1]));
            return true;
        }

        return false;
    }

    private static int? TryGetIntLiteral(ExpressionSyntax expression)
    {
        if (expression is LiteralExpressionSyntax literal && literal.Token.Value is int value)
        {
            return value;
        }

        return null;
    }

    private static async Task<Document> ApplyAsync(
        Document document,
        Dictionary<SyntaxNode, SyntaxNode> replacements,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var newRoot = root.ReplaceNodes(replacements.Keys, (original, _) => replacements[original]);
        return document.WithSyntaxRoot(newRoot);
    }
}
