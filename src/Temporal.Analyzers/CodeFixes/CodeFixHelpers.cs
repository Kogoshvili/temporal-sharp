using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Kogoshvili.Temporal.Analyzers.Analysis;

namespace Kogoshvili.Temporal.Analyzers.CodeFixes;

internal static class CodeFixHelpers
{
    public static async Task<Document> ReplaceWithAwaitAsync(
        Document document,
        SyntaxNode reported,
        ExpressionSyntax taskExpression,
        CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var function = EnclosingFunction(reported);
        var method = semanticModel is not null ? EnclosingMethod(semanticModel, reported) : null;

        var awaitExpression = SyntaxFactory.AwaitExpression(
            SyntaxFactory.Token(SyntaxKind.AwaitKeyword).WithTrailingTrivia(SyntaxFactory.Space),
            ParenthesizeForAwait(taskExpression.WithoutLeadingTrivia()));

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        if (function is not null)
        {
            var newFunction = function.ReplaceNode(reported, awaitExpression);
            if (method is { IsAsync: false })
            {
                newFunction = AddAsyncModifier(newFunction);
            }

            return document.WithSyntaxRoot(root.ReplaceNode(function, newFunction));
        }

        return document.WithSyntaxRoot(root.ReplaceNode(reported, awaitExpression));
    }

    public static ExpressionSyntax ParenthesizeForAwait(ExpressionSyntax expression) =>
        expression switch
        {
            IdentifierNameSyntax or
            MemberAccessExpressionSyntax or
            InvocationExpressionSyntax or
            ElementAccessExpressionSyntax or
            ParenthesizedExpressionSyntax or
            ThisExpressionSyntax or
            BaseExpressionSyntax => expression,
            _ => SyntaxFactory.ParenthesizedExpression(expression),
        };

    public static SyntaxNode AddAsyncModifier(SyntaxNode function)
    {
        var asyncToken = SyntaxFactory.Token(SyntaxKind.AsyncKeyword)
            .WithTrailingTrivia(SyntaxFactory.Space);

        return function switch
        {
            MethodDeclarationSyntax method => method.AddModifiers(asyncToken),
            LocalFunctionStatementSyntax local => local.AddModifiers(asyncToken),
            _ => function,
        };
    }

    public static IMethodSymbol? EnclosingMethod(SemanticModel model, SyntaxNode node)
    {
        var symbol = model.GetEnclosingSymbol(node.SpanStart);
        for (var current = symbol; current is not null; current = current.ContainingSymbol)
        {
            if (current is IMethodSymbol method)
            {
                return method;
            }
        }

        return null;
    }

    public static SyntaxNode? EnclosingFunction(SyntaxNode node) =>
        node.AncestorsAndSelf().FirstOrDefault(a => a is MethodDeclarationSyntax or LocalFunctionStatementSyntax);

    public static bool IsAsyncCompatibleReturn(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Void)
        {
            return true;
        }

        return TypeNames.FullName(type) is "System.Threading.Tasks.Task"
            or "System.Threading.Tasks.ValueTask"
            or "System.Collections.Generic.IAsyncEnumerable";
    }
}
