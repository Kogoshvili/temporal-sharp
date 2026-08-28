using Amazon.Runtime;
using Azure.Core;
using Kogoshvili.Temporal.Codec;
using Microsoft.Extensions.DependencyInjection;

namespace Kogoshvili.Temporal.Cloud;

/// <summary>
/// Registers the cloud secret resolvers and claim-check store factories in the
/// service container. The hosting starter selects a resolver by name when a
/// codec key or store credential is sourced from a vault.
/// </summary>
public static class SecretResolverExtensions
{
    /// <summary>
    /// Registers the <c>azureKeyVault</c> secret resolver using the default
    /// Azure credential chain.
    /// </summary>
    public static IServiceCollection AddAzureKeyVaultSecretResolver(
        this IServiceCollection services,
        string vaultUri,
        TokenCredential? credential = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultUri);
        services.AddSingleton<ISecretResolver>(
            new AzureKeyVaultSecretResolver(vaultUri, credential ?? AzureCredentialResolver.Resolve()));
        return services;
    }

    /// <summary>
    /// Registers the <c>awsSecretsManager</c> secret resolver using the default
    /// AWS credential chain.
    /// </summary>
    public static IServiceCollection AddAwsSecretsManagerSecretResolver(
        this IServiceCollection services,
        string region,
        AWSCredentials? credentials = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        services.AddSingleton<ISecretResolver>(
            new AwsSecretsManagerSecretResolver(credentials ?? AwsCredentialResolver.Resolve(), region));
        return services;
    }

    /// <summary>
    /// Registers the <c>azureBlob</c> claim-check store factory.
    /// </summary>
    public static IServiceCollection AddAzureBlobClaimCheckStore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IClaimCheckStoreFactory>(new AzureBlobClaimCheckStoreFactory());
        return services;
    }

    /// <summary>
    /// Registers the <c>s3</c> claim-check store factory.
    /// </summary>
    public static IServiceCollection AddS3ClaimCheckStore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IClaimCheckStoreFactory>(new S3ClaimCheckStoreFactory());
        return services;
    }
}
