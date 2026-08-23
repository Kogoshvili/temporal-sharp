using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Kogoshvili.Temporal.Analyzers.Analysis;

namespace Kogoshvili.Temporal.Analyzers.CodeFixes;

/// <summary>
/// Rewrites deny-listed BCL members into their deterministic <c>Workflow.*</c>
/// replacements:
/// <list type="bullet">
/// <item><c>DateTime.Now/UtcNow</c> → <c>Workflow.UtcNow</c> (TMP0101)</item>
/// <item><c>Guid.NewGuid()</c> → <c>Workflow.NewGuid()</c>, <c>new Random()</c> → <c>Workflow.Random</c> (TMP0121)</item>
/// <item><c>Task.Delay</c> → <c>Workflow.DelayAsync</c> (TMP0111)</item>
/// <item><c>Task.WhenAll/WhenAny</c> → <c>Workflow.WhenAllAsync/WhenAnyAsync</c> (TMP0143)</item>
/// <item><c>Task.Run</c> / <c>TaskFactory.StartNew</c> → <c>Workflow.RunTaskAsync</c> (TMP0146)</item>
/// </list>
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(WorkflowApiReplacementCodeFixProvider)), Shared]
public sealed class WorkflowApiReplacementCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("TMP0101", "TMP0111", "TMP0121", "TMP0143", "TMP0146", "TMP0148");

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
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        var replacement = GetReplacement(diagnostic.Id, node, semanticModel);
        if (replacement is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                replacement.Title,
                ct => ReplaceAsync(context.Document, node, replacement, ct),
                equivalenceKey: replacement.Title),
            diagnostic);
    }

    private sealed class Replacement
    {
        public Replacement(string title, ExpressionSyntax newExpression, bool replaceWholeNode)
        {
            Title = title;
            NewExpression = newExpression;
            ReplaceWholeNode = replaceWholeNode;
        }

        public string Title { get; }

        public ExpressionSyntax NewExpression { get; }

        public bool ReplaceWholeNode { get; }
    }

    private static Replacement? GetReplacement(string id, SyntaxNode node, SemanticModel model)
    {
        switch (id)
        {
            case "TMP0101":
                if (node is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Now" or "UtcNow" } clock &&
                    IsDateTime(model.GetTypeInfo(clock.Expression).Type))
                {
                    return new Replacement(
                        "Use Workflow.UtcNow",
                        CodeFixHelpers.QualifiedName("Temporalio", "Workflows", "Workflow", "UtcNow"),
                        true);
                }

                break;

            case "TMP0121":
                if (node is ObjectCreationExpressionSyntax && IsRandom(model.GetTypeInfo(node).Type))
                {
                    return new Replacement(
                        "Use Workflow.Random",
                        CodeFixHelpers.QualifiedName("Temporalio", "Workflows", "Workflow", "Random"),
                        true);
                }

                if (node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "NewGuid" } newGuidMember } &&
                    IsGuid(model.GetTypeInfo(newGuidMember.Expression).Type))
                {
                    return new Replacement(
                        "Use Workflow.NewGuid",
                        CodeFixHelpers.QualifiedName("Temporalio", "Workflows", "Workflow", "NewGuid"),
                        false);
                }

                break;

            case "TMP0111":
                if (node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Delay" } })
                {
                    return new Replacement(
                        "Use Workflow.DelayAsync",
                        CodeFixHelpers.QualifiedName("Temporalio", "Workflows", "Workflow", "DelayAsync"),
                        false);
                }

                break;

            case "TMP0143":
                if (node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "WhenAny" } })
                {
                    return new Replacement(
                        "Use Workflow.WhenAnyAsync",
                        CodeFixHelpers.QualifiedName("Temporalio", "Workflows", "Workflow", "WhenAnyAsync"),
                        false);
                }

                break;

            case "TMP0148":
                if (node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "WhenAll" } })
                {
                    return new Replacement(
                        "Use Workflow.WhenAllAsync",
                        CodeFixHelpers.QualifiedName("Temporalio", "Workflows", "Workflow", "WhenAllAsync"),
                        false);
                }

                break;

            case "TMP0146":
                if (node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Run" or "StartNew" } })
                {
                    return new Replacement(
                        "Use Workflow.RunTaskAsync",
                        CodeFixHelpers.QualifiedName("Temporalio", "Workflows", "Workflow", "RunTaskAsync"),
                        false);
                }

                break;
        }

        return null;
    }

    private static bool IsDateTime(ITypeSymbol? type) => type is not null && TypeNames.FullName(type) == "System.DateTime";

    private static bool IsGuid(ITypeSymbol? type) => type is not null && TypeNames.FullName(type) == "System.Guid";

    private static bool IsRandom(ITypeSymbol? type) => type is not null && TypeNames.FullName(type) == "System.Random";

    private static async Task<Document> ReplaceAsync(
        Document document,
        SyntaxNode node,
        Replacement replacement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        SyntaxNode newNode;
        if (replacement.ReplaceWholeNode)
        {
            newNode = replacement.NewExpression.WithTriviaFrom(node);
        }
        else if (node is InvocationExpressionSyntax invocation)
        {
            newNode = invocation.WithExpression(replacement.NewExpression);
        }
        else
        {
            return document;
        }

        return document.WithSyntaxRoot(root.ReplaceNode(node, newNode));
    }
}
