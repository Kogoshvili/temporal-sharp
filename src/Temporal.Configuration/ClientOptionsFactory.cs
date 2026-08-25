using Temporalio.Client;

namespace Kogoshvili.Temporal.Configuration;

/// <summary>
/// Builds <see cref="TemporalClientConnectOptions"/> from
/// <see cref="TemporalConnectionOptions"/>.
/// </summary>
public static class ClientOptionsFactory
{
    /// <summary>
    /// Applies connection options (target host, namespace, API key, TLS) to a
    /// connect-options instance.
    /// </summary>
    public static void Apply(TemporalClientConnectOptions connect, TemporalConnectionOptions options)
    {
        connect.TargetHost = options.TargetHost;
        connect.Namespace = options.Namespace;
        connect.ApiKey = options.ApiKey;
        connect.Tls = BuildTls(options.Tls);
        connect.RpcRetry = BuildRpcRetry(options.RpcRetry);
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

    private static TlsOptions? BuildTls(TemporalTlsOptions? tls)
    {
        if (tls is null)
        {
            return null;
        }

        if (tls.Disabled)
        {
            return new TlsOptions { Disabled = true };
        }

        return new TlsOptions
        {
            Domain = tls.Domain,
            ServerRootCACert = ReadAllBytes(tls.ServerRootCACertPath),
            ClientCert = ReadAllBytes(tls.ClientCertPath),
            ClientPrivateKey = ReadAllBytes(tls.ClientPrivateKeyPath),
        };
    }

    private static byte[]? ReadAllBytes(string? path) =>
        path is null ? null : File.ReadAllBytes(path);
}
