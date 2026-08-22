using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TemporalSharp.Analyzers.Analysis;

/// <summary>
/// Resolves the concrete method targeted by a typed lambda argument, e.g.
/// <c>Workflow.ExecuteActivityAsync(() => MyActivity.Run())</c> resolves to
/// <c>MyActivity.Run</c>.
/// </summary>
internal static class LambdaTargetResolver
{
    public static IMethodSymbol? ResolveTypedLambdaTarget(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            var expression = argument.Expression;
            while (expression is CastExpressionSyntax cast)
            {
                expression = cast.Expression;
            }

            if (expression is not LambdaExpressionSyntax lambda)
            {
                continue;
            }

            var body = lambda.Body;
            while (body is ParenthesizedExpressionSyntax parens)
            {
                body = parens.Expression;
            }

            if (body is InvocationExpressionSyntax bodyInvocation)
            {
                return context.SemanticModel.GetSymbolInfo(bodyInvocation).Symbol as IMethodSymbol;
            }
        }

        return null;
    }
}
