using Microsoft.Extensions.Logging;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.OperatorService.V1;

namespace Kogoshvili.Temporal.Hosting;

/// <inheritdoc cref="ISearchAttributeOps" />
public sealed class SearchAttributeOps : ISearchAttributeOps
{
    private readonly ITemporalClientFactory clients;
    private readonly ILogger<SearchAttributeOps> logger;

    /// <summary>Initializes a new instance of the <see cref="SearchAttributeOps"/> class.</summary>
    public SearchAttributeOps(ITemporalClientFactory clients, ILogger<SearchAttributeOps> logger)
    {
        this.clients = clients;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task EnsureAsync(
        string ns,
        IReadOnlyDictionary<string, IndexedValueType> declared,
        bool failOnConflict)
    {
        ArgumentNullException.ThrowIfNull(declared);
        if (declared.Count == 0)
        {
            return;
        }

        var existing = await ListAsync(ns).ConfigureAwait(false);
        var diff = Diff(declared, existing);

        foreach (var conflict in diff.Conflicts)
        {
            var message =
                $"Search attribute '{conflict.Name}' in namespace '{ns}' is declared as " +
                $"{conflict.Declared} but already exists as {conflict.Existing}.";

            if (failOnConflict)
            {
                throw new InvalidOperationException(message);
            }

            logger.LogWarning("{Message}", message);
        }

        if (diff.Missing.Count > 0)
        {
            var request = new AddSearchAttributesRequest { Namespace = ns };
            foreach (var (name, type) in diff.Missing)
            {
                request.SearchAttributes.Add(name, type);
            }

            await clients.Get(ns).OperatorService.AddSearchAttributesAsync(request).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, IndexedValueType>> ListAsync(string ns)
    {
        var response = await clients.Get(ns).OperatorService
            .ListSearchAttributesAsync(new ListSearchAttributesRequest { Namespace = ns })
            .ConfigureAwait(false);

        return response.CustomAttributes.ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string ns, IReadOnlyCollection<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        var request = new RemoveSearchAttributesRequest { Namespace = ns };
        foreach (var name in names)
        {
            request.SearchAttributes.Add(name);
        }

        return clients.Get(ns).OperatorService.RemoveSearchAttributesAsync(request);
    }

    /// <summary>
    /// Computes which declared attributes are missing and which conflict on type
    /// against the existing set. Pure and side-effect free, for unit testing.
    /// </summary>
    internal static SearchAttributeDiff Diff(
        IReadOnlyDictionary<string, IndexedValueType> declared,
        IReadOnlyDictionary<string, IndexedValueType> existing)
    {
        var missing = new Dictionary<string, IndexedValueType>();
        var conflicts = new List<SearchAttributeConflict>();

        foreach (var (name, type) in declared)
        {
            if (existing.TryGetValue(name, out var existingType))
            {
                if (existingType != type)
                {
                    conflicts.Add(new SearchAttributeConflict(name, type, existingType));
                }
            }
            else
            {
                missing[name] = type;
            }
        }

        return new SearchAttributeDiff(missing, conflicts);
    }
}

/// <summary>The result of reconciling declared attributes against existing ones.</summary>
internal readonly record struct SearchAttributeDiff(
    IReadOnlyDictionary<string, IndexedValueType> Missing,
    IReadOnlyList<SearchAttributeConflict> Conflicts);

/// <summary>A declared attribute whose type differs from the server's.</summary>
internal readonly record struct SearchAttributeConflict(
    string Name,
    IndexedValueType Declared,
    IndexedValueType Existing);
