using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Kogoshvili.Temporal.Analyzers.Analysis;
using Kogoshvili.Temporal.Analyzers.Diagnostics;

namespace Kogoshvili.Temporal.Analyzers.Analyzers;

/// <summary>
/// Experimental comment requirements for versioning and identity APIs:
/// TMP4201 (Workflow.NewGuid), TMP4202 (Workflow.DeprecatePatch), and
/// TMP4203 (Workflow.Patched with a replay-tested comment). All are opt-in.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CommentRuleAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.NewGuidRequiresComment,
            DiagnosticDescriptors.DeprecatePatchRequiresComment,
            DiagnosticDescriptors.VersioningChangeRequiresReplayComment);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
            method.ContainingType is null ||
            !SdkNames.IsWorkflowType(method.ContainingType))
        {
            return;
        }

        switch (method.Name)
        {
            case "NewGuid":
                ReportIfNoComment(context, invocation, DiagnosticDescriptors.NewGuidRequiresComment, null);
                break;
            case "DeprecatePatch":
                ReportIfNoComment(context, invocation, DiagnosticDescriptors.DeprecatePatchRequiresComment, null);
                break;
            case "Patched":
                ReportIfNoComment(context, invocation, DiagnosticDescriptors.VersioningChangeRequiresReplayComment, "replay");
                break;
        }
    }

    private static void ReportIfNoComment(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        DiagnosticDescriptor descriptor,
        string? requiredKeyword)
    {
        if (invocation.FirstAncestorOrSelf<StatementSyntax>() is { } statement &&
            HasComment(statement, requiredKeyword))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(descriptor, invocation.GetLocation()));
    }

    private static bool HasComment(StatementSyntax statement, string? requiredKeyword)
    {
        var trivia = statement.GetLeadingTrivia().Concat(statement.GetTrailingTrivia());
        foreach (var item in trivia)
        {
            if (item.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                item.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                if (requiredKeyword is null ||
                    item.ToString().IndexOf(requiredKeyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
