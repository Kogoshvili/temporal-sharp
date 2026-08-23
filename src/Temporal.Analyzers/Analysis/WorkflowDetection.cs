using Microsoft.CodeAnalysis;

namespace Kogoshvili.Temporal.Analyzers.Analysis;

/// <summary>
/// Identifies Temporal concepts (workflow types, workflow run methods, activity
/// methods) purely by attribute name, without referencing the Temporal SDK.
/// </summary>
internal static class WorkflowDetection
{
    public const string WorkflowAttributeName = "Temporalio.Workflows.WorkflowAttribute";
    public const string WorkflowRunAttributeName = "Temporalio.Workflows.WorkflowRunAttribute";
    public const string ActivityAttributeName = "Temporalio.Activities.ActivityAttribute";

    public static bool IsWorkflowType(INamedTypeSymbol type)
        => HasAttribute(type, WorkflowAttributeName);

    public static bool IsWorkflowRunMethod(IMethodSymbol method)
        => HasAttribute(method, WorkflowRunAttributeName);

    public static bool IsActivityMethod(IMethodSymbol method)
        => HasAttribute(method, ActivityAttributeName);

    public static bool HasActivityAttribute(ISymbol symbol)
        => HasAttribute(symbol, ActivityAttributeName);

    private static bool HasAttribute(ISymbol symbol, string attributeFullName)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is { } attributeClass &&
                attributeClass.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) == attributeFullName)
            {
                return true;
            }
        }

        return false;
    }
}
