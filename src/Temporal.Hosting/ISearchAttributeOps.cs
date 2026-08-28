using Temporalio.Api.Enums.V1;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Search-attribute operations facade over the SDK's operator service. The
/// <see cref="EnsureAsync(string, IReadOnlyDictionary{string, IndexedValueType}, bool)"/>
/// method is the idempotent core: it lists the namespace's attributes, adds any
/// declared attribute that is missing, and handles type conflicts by warning (or
/// throwing when <c>failOnConflict</c> is <c>true</c>). Removal is intentionally
/// separate so bootstrap never deletes attributes.
/// </summary>
public interface ISearchAttributeOps
{
    /// <summary>
    /// Ensures the declared attributes exist in the namespace, adding only the
    /// missing ones. Type conflicts warn (or throw when
    /// <paramref name="failOnConflict"/> is <c>true</c>). Never removes.
    /// </summary>
    Task EnsureAsync(
        string ns,
        IReadOnlyDictionary<string, IndexedValueType> declared,
        bool failOnConflict);

    /// <summary>
    /// Lists the custom search attributes (name to value type) in the namespace.
    /// </summary>
    Task<IReadOnlyDictionary<string, IndexedValueType>> ListAsync(string ns);

    /// <summary>
    /// Removes the given search attributes from the namespace. Not used by
    /// bootstrap; provided for parity with the operator service.
    /// </summary>
    Task RemoveAsync(string ns, IReadOnlyCollection<string> names);
}
