namespace Kogoshvili.Temporal.Configuration;

/// <summary>
/// TLS options for the Temporal connection. Certificates can come from one of
/// several sources, selected by <see cref="Source"/>:
/// <list type="bullet">
/// <item><description><c>file</c> (default) — PEM files read from the <c>*Path</c> properties.</description></item>
/// <item><description><c>environment</c> — PEM/base64 content read from the inline <c>ServerRootCACert</c>/<c>ClientCert</c>/<c>ClientPrivateKey</c> strings (typically injected as environment variables).</description></item>
/// <item><description><c>azureKeyVault</c> / <c>awsSecretsManager</c> — fetched asynchronously at startup by the hosting starter via <c>Kogoshvili.Temporal.Cloud</c>.</description></item>
/// </list>
/// </summary>
public sealed class TemporalTlsOptions
{
    /// <summary>Gets or sets a value indicating whether TLS is explicitly disabled.</summary>
    public bool Disabled { get; set; }

    /// <summary>Gets or sets the expected server hostname/domain for the certificate.</summary>
    public string? Domain { get; set; }

    /// <summary>
    /// Gets or sets the certificate source: <c>file</c>, <c>environment</c>,
    /// <c>azureKeyVault</c>, or <c>awsSecretsManager</c>. Default is <c>file</c>.
    /// </summary>
    public string Source { get; set; } = "file";

    /// <summary>Gets or sets the path to the server root CA certificate (PEM).</summary>
    public string? ServerRootCACertPath { get; set; }

    /// <summary>Gets or sets the path to the client certificate (PEM).</summary>
    public string? ClientCertPath { get; set; }

    /// <summary>Gets or sets the path to the client private key (PEM).</summary>
    public string? ClientPrivateKeyPath { get; set; }

    /// <summary>
    /// Gets or sets the server root CA certificate inline as base64 or raw PEM
    /// (used when <see cref="Source"/> is <c>environment</c>).
    /// </summary>
    public string? ServerRootCACert { get; set; }

    /// <summary>
    /// Gets or sets the client certificate inline as base64 or raw PEM
    /// (used when <see cref="Source"/> is <c>environment</c>).
    /// </summary>
    public string? ClientCert { get; set; }

    /// <summary>
    /// Gets or sets the client private key inline as base64 or raw PEM
    /// (used when <see cref="Source"/> is <c>environment</c>).
    /// </summary>
    public string? ClientPrivateKey { get; set; }

    /// <summary>Gets or sets Azure Key Vault configuration (used when <see cref="Source"/> is <c>azureKeyVault</c>).</summary>
    public AzureKeyVaultTlsOptions? AzureKeyVault { get; set; }

    /// <summary>Gets or sets AWS Secrets Manager configuration (used when <see cref="Source"/> is <c>awsSecretsManager</c>).</summary>
    public AwsSecretsManagerTlsOptions? AwsSecretsManager { get; set; }

    /// <summary>
    /// Validates the source selection and the mutual exclusivity of the file
    /// path and inline content properties.
    /// </summary>
    public void Validate()
    {
        if (Disabled)
        {
            if (Domain is not null ||
                ServerRootCACertPath is not null ||
                ClientCertPath is not null ||
                ClientPrivateKeyPath is not null ||
                ServerRootCACert is not null ||
                ClientCert is not null ||
                ClientPrivateKey is not null)
            {
                throw new InvalidOperationException(
                    "TLS cannot be disabled while certificate options are configured.");
            }

            return;
        }

        switch (Source)
        {
            case "file":
                if (ServerRootCACert is not null || ClientCert is not null || ClientPrivateKey is not null)
                {
                    throw new InvalidOperationException(
                        "Temporal:Tls inline certificate content is only valid when Source is 'environment'.");
                }

                break;

            case "environment":
                if (ServerRootCACertPath is not null || ClientCertPath is not null || ClientPrivateKeyPath is not null)
                {
                    throw new InvalidOperationException(
                        "Temporal:Tls certificate paths are only valid when Source is 'file'.");
                }

                break;

            case "azureKeyVault":
                if (AzureKeyVault is null ||
                    string.IsNullOrWhiteSpace(AzureKeyVault.VaultUri) ||
                    string.IsNullOrWhiteSpace(AzureKeyVault.CertificateName))
                {
                    throw new InvalidOperationException(
                        "Temporal:Tls:AzureKeyVault:VaultUri and CertificateName must be set when Source is 'azureKeyVault'.");
                }

                break;

            case "awsSecretsManager":
                if (AwsSecretsManager is null ||
                    string.IsNullOrWhiteSpace(AwsSecretsManager.CertificateSecretId) ||
                    string.IsNullOrWhiteSpace(AwsSecretsManager.PrivateKeySecretId))
                {
                    throw new InvalidOperationException(
                        "Temporal:Tls:AwsSecretsManager:CertificateSecretId and PrivateKeySecretId must be set when Source is 'awsSecretsManager'.");
                }

                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown Temporal:Tls:Source '{Source}'. Expected 'file', 'environment', 'azureKeyVault', or 'awsSecretsManager'.");
        }
    }
}

/// <summary>
/// Azure Key Vault certificate configuration for the <c>azureKeyVault</c> TLS
/// source. The certificate is stored as a PFX secret in Key Vault and converted
/// to PEM at startup.
/// </summary>
public sealed class AzureKeyVaultTlsOptions
{
    /// <summary>Gets or sets the vault URI (e.g. <c>https://my-vault.vault.azure.net</c>).</summary>
    public string? VaultUri { get; set; }

    /// <summary>Gets or sets the name of the secret holding the PFX certificate.</summary>
    public string? CertificateName { get; set; }

    /// <summary>Gets or sets the optional PFX password.</summary>
    public string? Password { get; set; }
}

/// <summary>
/// AWS Secrets Manager configuration for the <c>awsSecretsManager</c> TLS
/// source. The certificate and private key are stored as PEM text secrets.
/// </summary>
public sealed class AwsSecretsManagerTlsOptions
{
    /// <summary>Gets or sets the AWS region (e.g. <c>us-east-1</c>).</summary>
    public string? Region { get; set; }

    /// <summary>Gets or sets the secret id holding the client certificate (PEM or base64).</summary>
    public string? CertificateSecretId { get; set; }

    /// <summary>Gets or sets the secret id holding the client private key (PEM or base64).</summary>
    public string? PrivateKeySecretId { get; set; }

    /// <summary>Gets or sets the optional secret id holding the server root CA certificate.</summary>
    public string? ServerRootCACertSecretId { get; set; }
}
