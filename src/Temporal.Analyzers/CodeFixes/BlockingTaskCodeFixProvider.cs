using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kogoshvili.Temporal.Analyzers.CodeFixes;

/// <summary>
/// Converts a synchronous block on a task (TMP0111) into an <c>await</c>.
/// Handles <c>.Result</c>, <c>.Wait()</c>, and <c>.GetAwaiter().GetResult()</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BlockingTaskCodeFixProvider)), Shared]
public sealed class BlockingTaskCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("TMP0111");

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
        var taskExpression = GetTaskExpression(node);
        if (taskExpression is null || !await CanAwaitAsync(context.Document, node).ConfigureAwait(false))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use 'await'",
                ct => CodeFixHelpers.ReplaceWithAwaitAsync(context.Document, node, taskExpression, ct),
                equivalenceKey: "await"),
            diagnostic);
    }

    private static ExpressionSyntax? GetTaskExpression(SyntaxNode reported)
    {
        switch (reported)
        {
            case MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Result" } result:
                return result.Expression;

            case InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0 } invocation
                when invocation.Expression is MemberAccessExpressionSyntax memberAccess:
            {
                return memberAccess.Name.Identifier.ValueText switch
                {
                    "Wait" => memberAccess.Expression,
                    "GetResult" when memberAccess.Expression is InvocationExpressionSyntax getAwaiter
                        && getAwaiter.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "GetAwaiter" } getAwaiterMember =>
                        getAwaiterMember.Expression,
                    _ => null,
                };
            }

            default:
                return null;
        }
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
