using Amazon.Runtime;
using Azure.Core;
using Kogoshvili.Temporal.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kogoshvili.Temporal.Cloud;

/// <summary>
/// Registers the cloud TLS certificate sources in the service container. The
/// hosting starter selects one by <c>Temporal:Tls:Source</c>.
/// </summary>
public static class TlsCertificateSourceExtensions
{
    /// <summary>
    /// Registers the <c>azureKeyVault</c> TLS certificate source using the
    /// default Azure credential chain.
    /// </summary>
    public static IServiceCollection AddAzureKeyVaultCertificateSource(this IServiceCollection services, TokenCredential? credential = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ITlsCertificateSource>(
            new AzureKeyVaultCertificateSource(credential ?? AzureCredentialResolver.Resolve()));
        return services;
    }

    /// <summary>
    /// Registers the <c>awsSecretsManager</c> TLS certificate source using the
    /// default AWS credential chain. The region is read from the TLS options.
    /// </summary>
    public static IServiceCollection AddAwsSecretsManagerCertificateSource(this IServiceCollection services, AWSCredentials? credentials = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ITlsCertificateSource>(
            new AwsSecretsManagerCertificateSource(credentials ?? AwsCredentialResolver.Resolve()));
        return services;
    }
}
