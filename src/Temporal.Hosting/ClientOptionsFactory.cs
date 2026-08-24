using Temporalio.Client;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Builds <see cref="TemporalClientConnectOptions"/> from
/// <see cref="TemporalOptions"/>.
/// </summary>
internal static class ClientOptionsFactory
{
    public static void Apply(TemporalClientConnectOptions connect, TemporalOptions options)
    {
        connect.TargetHost = options.TargetHost;
        connect.Namespace = options.Namespace;
        connect.ApiKey = options.ApiKey;
        connect.Tls = BuildTls(options.Tls);
    }

    /// <summary>
    /// Creates the shared connect options for the test-server path. Its
    /// <see cref="TemporalClientConnectOptions.TargetHost"/> is left unset and
    /// filled in by <see cref="TemporalTestServerService"/> once the ephemeral
    /// dev server has bound a port.
    /// </summary>
    public static TemporalClientConnectOptions CreateTestServer(TemporalOptions options) =>
        new()
        {
            Namespace = options.Namespace,
        };

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
