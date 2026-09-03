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
    public const string WorkflowQueryAttributeName = "Temporalio.Workflows.WorkflowQueryAttribute";
    public const string WorkflowSignalAttributeName = "Temporalio.Workflows.WorkflowSignalAttribute";
    public const string WorkflowUpdateAttributeName = "Temporalio.Workflows.WorkflowUpdateAttribute";
    public const string WorkflowUpdateValidatorAttributeName = "Temporalio.Workflows.WorkflowUpdateValidatorAttribute";
    public const string WorkflowInitAttributeName = "Temporalio.Workflows.WorkflowInitAttribute";

    public static bool IsWorkflowType(INamedTypeSymbol type)
        => HasAttribute(type, WorkflowAttributeName);

    public static bool IsWorkflowRunMethod(IMethodSymbol method)
        => HasAttribute(method, WorkflowRunAttributeName);

    public static bool IsActivityMethod(IMethodSymbol method)
        => HasAttribute(method, ActivityAttributeName);

    public static bool HasActivityAttribute(ISymbol symbol)
        => HasAttribute(symbol, ActivityAttributeName);

    public static bool IsWorkflowQueryMethod(IMethodSymbol method)
        => HasAttribute(method, WorkflowQueryAttributeName);

    public static bool IsWorkflowQueryProperty(IPropertySymbol property)
        => HasAttribute(property, WorkflowQueryAttributeName);

    public static bool IsWorkflowSignalMethod(IMethodSymbol method)
        => HasAttribute(method, WorkflowSignalAttributeName);

    public static bool IsWorkflowUpdateMethod(IMethodSymbol method)
        => HasAttribute(method, WorkflowUpdateAttributeName);

    public static bool IsWorkflowUpdateValidatorMethod(IMethodSymbol method)
        => HasAttribute(method, WorkflowUpdateValidatorAttributeName);

    public static bool IsWorkflowInit(IMethodSymbol method)
        => HasAttribute(method, WorkflowInitAttributeName);

    /// <summary>
    /// Returns the name the SDK derives from the attributed symbol: the
    /// attribute's explicit name (constructor or <c>Name</c> property) when
    /// present, otherwise null (callers apply their own default, e.g. method
    /// or type name).
    /// </summary>
    public static string? GetAttributeName(ISymbol symbol, string attributeFullName)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attributeClass ||
                attributeClass.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) != attributeFullName)
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is string constructorName)
            {
                return constructorName;
            }

            foreach (var namedArgument in attribute.NamedArguments)
            {
                if (namedArgument.Key == "Name" && namedArgument.Value.Value is string namedName)
                {
                    return namedName;
                }
            }

            return null;
        }

        return null;
    }

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
