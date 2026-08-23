using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kogoshvili.Temporal.Analyzers.CodeFixes;

/// <summary>
/// Fixes a discarded (fire-and-forget) task call (TMP0112) by either awaiting it
/// or discarding the result explicitly with <c>_ =</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FloatingTaskCodeFixProvider)), Shared]
public sealed class FloatingTaskCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("TMP0112");

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
            invocation.Parent is not ExpressionStatementSyntax statement)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Discard with '_ ='",
                ct => DiscardAsync(context.Document, statement, invocation, ct),
                equivalenceKey: "discard"),
            diagnostic);

        if (await CanAwaitAsync(context.Document, invocation).ConfigureAwait(false))
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add 'await'",
                    ct => CodeFixHelpers.ReplaceWithAwaitAsync(context.Document, invocation, invocation, ct),
                    equivalenceKey: "await"),
                diagnostic);
        }
    }

    private static async Task<Document> DiscardAsync(
        Document document,
        ExpressionStatementSyntax statement,
        InvocationExpressionSyntax invocation,
        System.Threading.CancellationToken cancellationToken)
    {
        var discardAssignment = SyntaxFactory.AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxFactory.IdentifierName(SyntaxFactory.Identifier("_")),
            SyntaxFactory.Token(SyntaxKind.EqualsToken)
                .WithLeadingTrivia(SyntaxFactory.Space)
                .WithTrailingTrivia(SyntaxFactory.Space),
            invocation.WithoutLeadingTrivia());

        var newStatement = SyntaxFactory.ExpressionStatement(discardAssignment);

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return root is null
            ? document
            : document.WithSyntaxRoot(root.ReplaceNode(statement, newStatement));
    }

    private static async Task<bool> CanAwaitAsync(Document document, SyntaxNode reported)
    {
        var function = CodeFixHelpers.EnclosingFunction(reported);
        if (function is null)
        {
            return false;
        }

        var semanticModel = await document.GetSemanticModelAsync().ConfigureAwait(false);
        if (semanticModel is null)
        {
            return false;
        }

        var method = CodeFixHelpers.EnclosingMethod(semanticModel, reported);
        if (method is null)
        {
            return false;
        }

        return method.IsAsync || CodeFixHelpers.IsAsyncCompatibleReturn(method.ReturnType);
    }
}
