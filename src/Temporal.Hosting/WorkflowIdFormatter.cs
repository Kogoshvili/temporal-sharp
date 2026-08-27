using System.Text.RegularExpressions;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Formats workflow-ID templates. Shared by the client-side
/// <see cref="WorkflowOptionsRegistry"/> and the workflow-side
/// <see cref="ChildWorkflowOps"/> so both use the same placeholder semantics.
/// </summary>
internal static class WorkflowIdFormatter
{
    private static readonly Regex GuidPlaceholder = new(
        @"\{Guid(?::(?<format>[NDBPX]))?\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TypePlaceholder = new(
        @"\{Type(?::(?<mod>s))?\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Substitutes the <c>{Type}</c> (optionally <c>{Type:s}</c>),
    /// <c>{Queue}</c>, <c>{Parent}</c>, and <c>{Guid}</c> placeholders in
    /// <paramref name="format"/>. The <c>s</c> modifier strips a trailing
    /// "workflow" (case-insensitive) from the type name.
    /// </summary>
    public static string Format(string format, string workflowType, string? taskQueue = null, string? parentId = null)
    {
        var id = GuidPlaceholder.Replace(
            format,
            match => Guid.NewGuid().ToString(match.Groups["format"].Success ? match.Groups["format"].Value : "N"));

        id = TypePlaceholder.Replace(
            id,
            match => match.Groups["mod"].Success ? StripWorkflowSuffix(workflowType) : workflowType);

        return id
            .Replace("{Queue}", taskQueue ?? string.Empty, StringComparison.Ordinal)
            .Replace("{Parent}", parentId ?? string.Empty, StringComparison.Ordinal);
    }

    private static string StripWorkflowSuffix(string type) =>
        type.EndsWith("workflow", StringComparison.OrdinalIgnoreCase)
            ? type[..^"workflow".Length]
            : type;
}
