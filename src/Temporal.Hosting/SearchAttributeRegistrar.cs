using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Temporalio.Api.Enums.V1;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Registers search attributes declared in <c>Temporal:SearchAttributes</c> at
/// startup, idempotently, before workers poll. For each namespace (the default
/// plus every entry in <c>Temporal:Namespaces</c>) it lists the existing
/// attributes and adds only the missing ones. Runs after the connection waiter
/// so the server is reachable before registration.
/// </summary>
public sealed class SearchAttributeRegistrar : IHostedService
{
    private readonly IOptionsMonitor<TemporalOptions> options;
    private readonly ISearchAttributeOps searchAttributeOps;

    /// <summary>Initializes a new instance of the <see cref="SearchAttributeRegistrar"/> class.</summary>
    public SearchAttributeRegistrar(
        IOptionsMonitor<TemporalOptions> options,
        ISearchAttributeOps searchAttributeOps)
    {
        this.options = options;
        this.searchAttributeOps = searchAttributeOps;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var temporal = options.CurrentValue;
        var config = temporal.SearchAttributes;

        if (config is null || !config.Enabled || config.Attributes is not { Count: > 0 } attributes)
        {
            return;
        }

        var declared = attributes.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Type,
            StringComparer.Ordinal);

        foreach (var ns in ResolveNamespaces(temporal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await searchAttributeOps.EnsureAsync(ns, declared, config.FailOnConflict)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static IEnumerable<string> ResolveNamespaces(TemporalOptions temporal)
    {
        yield return temporal.Namespace;

        if (temporal.Namespaces is not { } namespaces)
        {
            yield break;
        }

        foreach (var ns in namespaces)
        {
            if (!string.Equals(ns, temporal.Namespace, StringComparison.Ordinal))
            {
                yield return ns;
            }
        }
    }
}
