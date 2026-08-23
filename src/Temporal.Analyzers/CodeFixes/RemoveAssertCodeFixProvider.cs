using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kogoshvili.Temporal.Analyzers.CodeFixes;

/// <summary>
/// Removes a <c>Debug.Assert</c> / <c>Trace.Assert</c> statement (TMP2133) from
/// production workflow code.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RemoveAssertCodeFixProvider)), Shared]
public sealed class RemoveAssertCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("TMP2133");

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
        if (node is InvocationExpressionSyntax invocation &&
            invocation.Parent is ExpressionStatementSyntax statement &&
            statement.Parent is BlockSyntax)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the assertion",
                    ct => RemoveAsync(context.Document, statement, ct),
                    equivalenceKey: "remove"),
                diagnostic);
        }
    }

    private static async Task<Document> RemoveAsync(
        Document document,
        ExpressionStatementSyntax statement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var newRoot = root.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
        return newRoot is null ? document : document.WithSyntaxRoot(newRoot);
    }
}
