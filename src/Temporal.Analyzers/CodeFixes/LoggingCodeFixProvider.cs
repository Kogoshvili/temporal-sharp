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
/// Rewrites non-replay-aware logging into the SDK logger:
/// <list type="bullet">
/// <item>TMP2131 (workflow): <c>Console/Debug/Trace.*</c> → <c>Workflow.Logger.*</c></item>
/// <item>TMP3106 (activity): <c>Console/Debug/Trace.*</c> → <c>ActivityExecutionContext.Current.Logger.*</c></item>
/// </list>
/// The target method name is mapped by receiver kind (Write/WriteLine →
/// LogInformation, Console.Error → LogError, Debug → LogDebug, Trace →
/// LogTrace). The <c>WriteLineIf</c>/<c>WriteIf</c> overloads are skipped.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LoggingCodeFixProvider)), Shared]
public sealed class LoggingCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("TMP2131", "TMP3106");

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
            invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        var logMethod = MapLogMethod(memberAccess, semanticModel);
        if (logMethod is null)
        {
            return;
        }

        var receiver = diagnostic.Id == "TMP3106"
            ? CodeFixHelpers.QualifiedName("Temporalio", "Activities", "ActivityExecutionContext", "Current", "Logger")
            : CodeFixHelpers.QualifiedName("Temporalio", "Workflows", "Workflow", "Logger");

        var newCallee = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            receiver,
            SyntaxFactory.IdentifierName(logMethod));

        var title = diagnostic.Id == "TMP3106" ? "Use ActivityExecutionContext.Current.Logger" : "Use Workflow.Logger";

        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                ct => ReplaceAsync(context.Document, invocation, newCallee, ct),
                equivalenceKey: logMethod),
            diagnostic);
    }

    private static string? MapLogMethod(MemberAccessExpressionSyntax memberAccess, SemanticModel model)
    {
        var receiverSymbol = model.GetSymbolInfo(memberAccess.Expression).Symbol;
        var methodName = memberAccess.Name.Identifier.ValueText;

        if (receiverSymbol is IPropertySymbol { Name: "Error" } error &&
            error.ContainingType is not null &&
            TypeNames.FullName(error.ContainingType) == "System.Console")
        {
            return methodName is "Write" or "WriteLine" ? "LogError" : null;
        }

        if (receiverSymbol is not INamedTypeSymbol receiverType)
        {
            return null;
        }

        return TypeNames.FullName(receiverType) switch
        {
            "System.Console" => methodName is "Write" or "WriteLine" ? "LogInformation" : null,
            "System.Diagnostics.Debug" => methodName is "Write" or "WriteLine" ? "LogDebug" : null,
            "System.Diagnostics.Trace" => methodName switch
            {
                "Write" or "WriteLine" => "LogTrace",
                "TraceError" => "LogError",
                "TraceWarning" => "LogWarning",
                "TraceInformation" => "LogInformation",
                _ => null,
            },
            _ => null,
        };
    }

    private static async Task<Document> ReplaceAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        ExpressionSyntax newCallee,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        return document.WithSyntaxRoot(root.ReplaceNode(invocation, invocation.WithExpression(newCallee)));
    }
}
