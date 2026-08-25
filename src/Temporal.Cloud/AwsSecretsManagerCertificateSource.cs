using Amazon;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Kogoshvili.Temporal.Configuration;

namespace Kogoshvili.Temporal.Cloud;

/// <summary>
/// Resolves TLS certificate material from AWS Secrets Manager. The client
/// certificate and private key are stored as PEM (or base64 PEM) secrets.
/// </summary>
public sealed class AwsSecretsManagerCertificateSource : ITlsCertificateSource
{
    private readonly IAmazonSecretsManager? secretsManager;
    private readonly AWSCredentials? credentials;

    /// <summary>
    /// Initializes a new instance of the <see cref="AwsSecretsManagerCertificateSource"/> class
    /// from an existing client (useful for testing or pre-configured clients).
    /// </summary>
    public AwsSecretsManagerCertificateSource(IAmazonSecretsManager secretsManager)
    {
        ArgumentNullException.ThrowIfNull(secretsManager);
        this.secretsManager = secretsManager;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AwsSecretsManagerCertificateSource"/> class
    /// using the given credentials; the region is read from the TLS options.
    /// </summary>
    public AwsSecretsManagerCertificateSource(AWSCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        this.credentials = credentials;
    }

    /// <inheritdoc />
    public string Name => "awsSecretsManager";

    /// <inheritdoc />
    public async Task<TlsCertificateMaterial> ResolveAsync(TemporalTlsOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var aws = options.AwsSecretsManager
            ?? throw new InvalidOperationException("Temporal:Tls:AwsSecretsManager must be configured when using the 'awsSecretsManager' TLS source.");

        var client = secretsManager ?? new AmazonSecretsManagerClient(credentials, RegionEndpoint.GetBySystemName(aws.Region));

        var clientCert = TlsContent.Decode(await GetSecretStringAsync(client, aws.CertificateSecretId!, cancellationToken).ConfigureAwait(false));
        var clientPrivateKey = TlsContent.Decode(await GetSecretStringAsync(client, aws.PrivateKeySecretId!, cancellationToken).ConfigureAwait(false));
        var serverRootCACert = aws.ServerRootCACertSecretId is null
            ? null
            : TlsContent.Decode(await GetSecretStringAsync(client, aws.ServerRootCACertSecretId, cancellationToken).ConfigureAwait(false));

        return new TlsCertificateMaterial(serverRootCACert, clientCert, clientPrivateKey);
    }

    private static async Task<string?> GetSecretStringAsync(IAmazonSecretsManager client, string secretId, CancellationToken cancellationToken)
    {
        var response = await client.GetSecretValueAsync(new GetSecretValueRequest { SecretId = secretId }, cancellationToken).ConfigureAwait(false);
        return response.SecretString ?? (response.SecretBinary is null ? null : Convert.ToBase64String(response.SecretBinary.ToArray()));
    }
}
