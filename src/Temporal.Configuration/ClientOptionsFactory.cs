using Temporalio.Client;

namespace Kogoshvili.Temporal.Configuration;

/// <summary>
/// Builds <see cref="TemporalClientConnectOptions"/> from
/// <see cref="TemporalConnectionOptions"/>.
/// </summary>
public static class ClientOptionsFactory
{
    /// <summary>
    /// Applies connection options (target host, namespace, API key, TLS, RPC
    /// retry, keep-alive, HTTP proxy, DNS load balancing, and gRPC compression)
    /// to a connect-options instance. TLS is resolved synchronously for the
    /// <c>file</c> and <c>environment</c> sources; cloud sources
    /// (<c>azureKeyVault</c>/<c>awsSecretsManager</c>) are skipped here and
    /// resolved asynchronously by the hosting starter's certificate loader.
    /// </summary>
    public static void Apply(TemporalClientConnectOptions connect, TemporalConnectionOptions options)
    {
        connect.TargetHost = options.TargetHost;
        connect.Namespace = options.Namespace;
        connect.ApiKey = options.ApiKey;
        connect.Tls = BuildTls(options.Tls);
        connect.RpcRetry = BuildRpcRetry(options.RpcRetry);
        if (options.KeepAlive is { } keepAlive)
        {
            connect.KeepAlive = BuildKeepAlive(keepAlive);
        }

        if (options.HttpConnectProxy is { } proxy)
        {
            connect.HttpConnectProxy = BuildHttpConnectProxy(proxy);
        }

        if (options.DnsLoadBalancing is { } dns)
        {
            connect.DnsLoadBalancing = BuildDnsLoadBalancing(dns);
        }

        if (options.GrpcCompression is { } grpcCompression)
        {
            connect.GrpcCompression = BuildGrpcCompression(grpcCompression);
        }
    }

    /// <summary>
    /// Builds the SDK <see cref="TlsOptions"/> from already-resolved PEM
    /// material. Used by the hosting starter's certificate loader for the cloud
    /// sources, but suitable for any pre-resolved material.
    /// </summary>
    public static TlsOptions BuildTls(TlsCertificateMaterial material, TemporalTlsOptions tls)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(tls);

        if (tls.Disabled)
        {
            return new TlsOptions { Disabled = true };
        }

        return new TlsOptions
        {
            Domain = tls.Domain,
            ServerRootCACert = material.ServerRootCACert,
            ClientCert = material.ClientCert,
            ClientPrivateKey = material.ClientPrivateKey,
        };
    }

    /// <summary>
    /// Builds the SDK <see cref="TlsOptions"/> from configuration, resolving the
    /// <c>file</c> and <c>environment</c> sources synchronously. Returns
    /// <c>null</c> when TLS is not configured or when a cloud source is used
    /// (which must be resolved asynchronously).
    /// </summary>
    public static TlsOptions? BuildTls(TemporalTlsOptions? tls)
    {
        if (tls is null)
        {
            return null;
        }

        tls.Validate();

        var material = tls.Source switch
        {
            "file" => new TlsCertificateMaterial(
                ReadAllBytes(tls.ServerRootCACertPath),
                ReadAllBytes(tls.ClientCertPath),
                ReadAllBytes(tls.ClientPrivateKeyPath)),
            "environment" => new TlsCertificateMaterial(
                TlsContent.Decode(tls.ServerRootCACert),
                TlsContent.Decode(tls.ClientCert),
                TlsContent.Decode(tls.ClientPrivateKey)),
            _ => null,
        };

        return material is null ? null : BuildTls(material, tls);
    }

    private static RpcRetryOptions? BuildRpcRetry(TemporalRpcRetryOptions? rpcRetry)
    {
        if (rpcRetry is null)
        {
            return null;
        }

        return new RpcRetryOptions
        {
            InitialInterval = rpcRetry.InitialInterval,
            RandomizationFactor = rpcRetry.RandomizationFactor,
            Multiplier = rpcRetry.Multiplier,
            MaxInterval = rpcRetry.MaxInterval,
            MaxElapsedTime = rpcRetry.MaxElapsedTime,
            MaxRetries = rpcRetry.MaxRetries,
        };
    }

    private static KeepAliveOptions BuildKeepAlive(TemporalKeepAliveOptions keepAlive) =>
        new()
        {
            Interval = keepAlive.Interval,
            Timeout = keepAlive.Timeout,
        };

    private static HttpConnectProxyOptions? BuildHttpConnectProxy(TemporalHttpConnectProxyOptions proxy) =>
        proxy.TargetHost is null
            ? null
            : new HttpConnectProxyOptions(proxy.TargetHost)
            {
                BasicAuth = proxy.Username is null || proxy.Password is null
                    ? null
                    : (proxy.Username, proxy.Password),
            };

    private static DnsLoadBalancingOptions BuildDnsLoadBalancing(TemporalDnsLoadBalancingOptions dns) =>
        new() { ResolutionInterval = dns.ResolutionInterval };

    private static GrpcCompression BuildGrpcCompression(TemporalGrpcCompressionOptions compression) =>
        compression.Mode switch
        {
            TemporalGrpcCompressionOptions.Gzip => new GrpcCompression.Gzip(),
            TemporalGrpcCompressionOptions.None => new GrpcCompression.None(),
            _ => throw new InvalidOperationException(
                $"Temporal:GrpcCompression:Mode must be '{TemporalGrpcCompressionOptions.Gzip}' or '{TemporalGrpcCompressionOptions.None}'."),
        };

    private static byte[]? ReadAllBytes(string? path) =>
        path is null ? null : File.ReadAllBytes(path);
}
