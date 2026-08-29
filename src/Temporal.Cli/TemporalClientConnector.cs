using Kogoshvili.Temporal.Cloud;
using Kogoshvili.Temporal.Configuration;
using Microsoft.Extensions.Configuration;
using Temporalio.Client;

namespace Kogoshvili.Temporal.Cli;

/// <summary>
/// Builds a <see cref="ITemporalClient"/> from shared configuration
/// (<c>appsettings.json</c> + <c>Temporal__*</c> environment variables),
/// resolving the cloud TLS certificate sources (<c>azureKeyVault</c> /
/// <c>awsSecretsManager</c>) that <see cref="ClientOptionsFactory"/> handles
/// only synchronously. Mirrors the hosting starter's
/// <c>TemporalCertificateLoader</c> without the DI container.
/// </summary>
internal static class TemporalClientConnector
{
    public static async Task<ITemporalClient> ConnectAsync(
        IConfiguration configuration,
        IEnumerable<ITlsCertificateSource>? sources = null,
        CancellationToken cancellationToken = default)
    {
        var options = TemporalConfig.Load(configuration);
        var connect = TemporalConfig.ToConnectOptions(options);

        var tls = options.Tls;
        if (tls is not null && !tls.Disabled && tls.Source is "azureKeyVault" or "awsSecretsManager")
        {
            connect.Tls = await ResolveCloudTlsAsync(tls, sources, cancellationToken).ConfigureAwait(false);
        }

        return await TemporalClient.ConnectAsync(connect).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves TLS options for a cloud certificate source by matching the
    /// configured <see cref="TemporalTlsOptions.Source"/> name against the
    /// available sources. Exposed for testing without a live connection.
    /// </summary>
    internal static async Task<TlsOptions> ResolveCloudTlsAsync(
        TemporalTlsOptions tls,
        IEnumerable<ITlsCertificateSource>? sources,
        CancellationToken cancellationToken)
    {
        var available = sources ?? new ITlsCertificateSource[]
        {
            new AzureKeyVaultCertificateSource(AzureCredentialResolver.Resolve()),
            new AwsSecretsManagerCertificateSource(AwsCredentialResolver.Resolve()),
        };

        var source = available.FirstOrDefault(s => s.Name == tls.Source)
            ?? throw new InvalidOperationException(
                $"No TLS certificate source named '{tls.Source}' is available.");

        var material = await source.ResolveAsync(tls, cancellationToken).ConfigureAwait(false);
        return ClientOptionsFactory.BuildTls(material, tls);
    }
}
