using Amazon;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Kogoshvili.Temporal.Codec;

namespace Kogoshvili.Temporal.Cloud;

/// <summary>
/// Resolves secrets from AWS Secrets Manager. The region is fixed at
/// construction; each call resolves a single secret by id. Binary secrets are
/// returned as their base64 encoding.
/// </summary>
public sealed class AwsSecretsManagerSecretResolver : ISecretResolver
{
    private readonly IAmazonSecretsManager? secretsManager;
    private readonly AWSCredentials? credentials;
    private readonly string region;

    /// <summary>
    /// Initializes a new instance of the <see cref="AwsSecretsManagerSecretResolver"/> class
    /// from an existing client (useful for testing or pre-configured clients).
    /// </summary>
    public AwsSecretsManagerSecretResolver(IAmazonSecretsManager secretsManager)
    {
        ArgumentNullException.ThrowIfNull(secretsManager);
        this.secretsManager = secretsManager;
        region = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AwsSecretsManagerSecretResolver"/> class
    /// using the given credentials and region.
    /// </summary>
    public AwsSecretsManagerSecretResolver(AWSCredentials credentials, string region)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        this.credentials = credentials;
        this.region = region;
    }

    /// <inheritdoc />
    public string Name => "awsSecretsManager";

    /// <inheritdoc />
    public async Task<string> ResolveAsync(string secretId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretId);
        var client = secretsManager ?? new AmazonSecretsManagerClient(credentials, RegionEndpoint.GetBySystemName(region));

        var response = await client.GetSecretValueAsync(new GetSecretValueRequest { SecretId = secretId }, cancellationToken).ConfigureAwait(false);
        return response.SecretString
            ?? (response.SecretBinary is null ? string.Empty : Convert.ToBase64String(response.SecretBinary.ToArray()));
    }
}
