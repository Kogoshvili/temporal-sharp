using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Temporalio.Client;
using Temporalio.Client.Interceptors;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Default <see cref="ITemporalClientFactory"/>. Owns a single lazy
/// <see cref="TemporalConnection"/> (optionally supplied) and fans out
/// namespace-scoped <see cref="TemporalClient"/> instances over it, caching
/// each namespace's client on first use.
/// </summary>
internal sealed class TemporalClientFactory : ITemporalClientFactory
{
    private readonly TemporalClientConnectOptions connectOptions;
    private readonly Func<string, IReadOnlyCollection<IClientInterceptor>?> interceptorFactory;
    private readonly ITemporalConnection? suppliedConnection;
    private readonly Lazy<ITemporalConnection> connection;
    private readonly ConcurrentDictionary<string, ITemporalClient> clients = new(StringComparer.Ordinal);
    private readonly string defaultNamespace;

    public TemporalClientFactory(
        TemporalClientConnectOptions connectOptions,
        Func<string, IReadOnlyCollection<IClientInterceptor>?> interceptorFactory,
        ITemporalConnection? connection = null)
    {
        this.connectOptions = connectOptions;
        this.interceptorFactory = interceptorFactory;
        this.suppliedConnection = connection;
        this.defaultNamespace = connectOptions.Namespace;
        this.connection = new Lazy<ITemporalConnection>(
            () => suppliedConnection ?? TemporalConnection.CreateLazy(connectOptions));
    }

    public ITemporalClient Get(string? ns = null)
    {
        var actual = string.IsNullOrWhiteSpace(ns) ? defaultNamespace : ns;
        return clients.GetOrAdd(actual, CreateClient);
    }

    private ITemporalClient CreateClient(string ns) =>
        new TemporalClient(
            connection.Value,
            new TemporalClientOptions
            {
                Namespace = ns,
                DataConverter = connectOptions.DataConverter,
                Interceptors = interceptorFactory(ns),
                LoggerFactory = connectOptions.LoggerFactory ?? NullLoggerFactory.Instance,
                QueryRejectCondition = connectOptions.QueryRejectCondition,
            });
}

/// <summary>
/// <see cref="ITemporalClientFactory"/> that always returns the same
/// pre-built client, used when a caller supplies an <see cref="ITemporalClient"/>
/// directly (the "configure everything yourself" escape hatch).
/// </summary>
internal sealed class StaticTemporalClientFactory : ITemporalClientFactory
{
    private readonly ITemporalClient client;

    public StaticTemporalClientFactory(ITemporalClient client) => this.client = client;

    public ITemporalClient Get(string? ns = null) => client;
}

/// <summary>
/// <see cref="ITemporalClientFactory"/> that resolves a client through a
/// user-supplied delegate, used when a caller supplies
/// <c>Func&lt;IServiceProvider, ITemporalClient&gt;</c>.
/// </summary>
internal sealed class DelegateTemporalClientFactory : ITemporalClientFactory
{
    private readonly Func<IServiceProvider, ITemporalClient> factory;
    private readonly IServiceProvider provider;
    private ITemporalClient? cached;

    public DelegateTemporalClientFactory(
        Func<IServiceProvider, ITemporalClient> factory,
        IServiceProvider provider)
    {
        this.factory = factory;
        this.provider = provider;
    }

    public ITemporalClient Get(string? ns = null) => cached ??= factory(provider);
}
