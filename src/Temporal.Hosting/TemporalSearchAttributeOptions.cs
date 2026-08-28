using Temporalio.Api.Enums.V1;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Search-attribute bootstrap configuration, bound from
/// <c>Temporal:SearchAttributes</c>. Declares custom search attributes to be
/// registered idempotently on the server at startup (before workers poll) via
/// <see cref="SearchAttributeRegistrar"/>.
/// </summary>
public sealed class TemporalSearchAttributesOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether search-attribute registration
    /// runs at startup. Defaults to <c>true</c>. Disable when an environment
    /// should not create attributes (e.g. it lacks operator permission).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether a type conflict between a
    /// declared attribute and an already-existing attribute fails startup
    /// rather than logging a warning. Defaults to <c>false</c> (warn and
    /// continue).
    /// </summary>
    public bool FailOnConflict { get; set; }

    /// <summary>
    /// Gets or sets the attributes to declare, keyed by name and bound from
    /// <c>Temporal:SearchAttributes:Attributes</c>.
    /// </summary>
    public Dictionary<string, TemporalSearchAttributeOptions>? Attributes { get; set; }
}

/// <summary>
/// A single search attribute declaration, bound from
/// <c>Temporal:SearchAttributes:Attributes:&lt;name&gt;</c>.
/// </summary>
public sealed class TemporalSearchAttributeOptions
{
    /// <summary>
    /// Gets or sets the indexed value type, bound from
    /// <c>Temporal:SearchAttributes:Attributes:&lt;name&gt;:Type</c>.
    /// </summary>
    public IndexedValueType Type { get; set; }
}
