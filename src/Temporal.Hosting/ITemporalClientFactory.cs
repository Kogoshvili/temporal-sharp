using Temporalio.Client;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Resolves a <see cref="ITemporalClient"/> for a Temporal namespace. A single
/// shared connection backs every namespace, so clients are cheap to create and
/// cached per namespace. The default namespace (from
/// <c>Temporal:Namespace</c>) is used when <c>ns</c> is <c>null</c> or empty.
/// </summary>
public interface ITemporalClientFactory
{
    /// <summary>
    /// Gets the client for the given namespace, creating it (lazily, over the
    /// shared connection) on first use and caching it thereafter.
    /// </summary>
    /// <param name="ns">
    /// Namespace to resolve, or <c>null</c>/empty for the default namespace.
    /// </param>
    /// <returns>The namespace-scoped client.</returns>
    ITemporalClient Get(string? ns = null);
}
