namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Payload-codec configuration for the starter. The codecs built from this
/// section are composed into a single <c>DataConverter</c> that is applied to
/// the client (and therefore every worker, which inherits the client's
/// converter). Both features are opt-in; when neither is enabled the SDK's
/// default data converter is used unchanged.
/// </summary>
public sealed class TemporalDataConverterOptions
{
    /// <summary>Gets or sets end-to-end payload encryption configuration.</summary>
    public TemporalEncryptionCodecOptions Encryption { get; set; } = new();

    /// <summary>Gets or sets claim-check (large-payload offload) configuration.</summary>
    public TemporalClaimCheckCodecOptions ClaimCheck { get; set; } = new();
}

/// <summary>
/// AES-GCM encryption codec configuration.
/// </summary>
public sealed class TemporalEncryptionCodecOptions
{
    /// <summary>Gets or sets a value indicating whether payload encryption is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the key source: <c>config</c> (inline <see cref="Key"/>),
    /// <c>azureKeyVault</c>, or <c>awsSecretsManager</c>. Default is <c>config</c>.
    /// </summary>
    public string Source { get; set; } = "config";

    /// <summary>
    /// Gets or sets the AES-GCM key as an ASCII string (16, 24, or 32 bytes). In
    /// production prefer a key-management system; this string form is provided
    /// for configuration and demo convenience and only applies when
    /// <see cref="Source"/> is <c>config</c>.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>Gets or sets the key id stamped into each payload for key rotation.</summary>
    public string KeyId { get; set; } = "default";

    /// <summary>
    /// Gets or sets the secret name (Azure Key Vault) or secret id (AWS Secrets
    /// Manager) holding the key, used when <see cref="Source"/> is a vault.
    /// </summary>
    public string? SecretId { get; set; }

    /// <summary>
    /// Gets or sets how the secret decodes into the key bytes: <c>raw</c>
    /// (ASCII), <c>base64</c>, or <c>hex</c>. Default is <c>raw</c>.
    /// </summary>
    public string Encoding { get; set; } = "raw";
}

/// <summary>
/// Claim-check (large-payload offload) codec configuration. Payloads larger than
/// <see cref="ThresholdBytes"/> are written to a store and replaced by a
/// reference in the workflow history.
/// </summary>
public sealed class TemporalClaimCheckCodecOptions
{
    /// <summary>Gets or sets a value indicating whether claim-check offloading is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the size threshold in bytes above which payloads are offloaded.
    /// Default is one megabyte.
    /// </summary>
    public int ThresholdBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// Gets or sets the store type: <c>filesystem</c>, <c>azureBlob</c>, or
    /// <c>s3</c>. Default is <c>filesystem</c>. Cloud stores are built via a
    /// registered <c>IClaimCheckStoreFactory</c> (see Kogoshvili.Temporal.Cloud).
    /// </summary>
    public string Store { get; set; } = "filesystem";

    /// <summary>
    /// Gets or sets the directory the filesystem claim-check store writes blobs
    /// to (used when <see cref="Store"/> is <c>filesystem</c>). Defaults to
    /// <c>claim-check</c>.
    /// </summary>
    public string Directory { get; set; } = "claim-check";

    /// <summary>
    /// Gets or sets the Azure Blob storage account URI (managed-identity auth),
    /// used when <see cref="Store"/> is <c>azureBlob</c>.
    /// </summary>
    public string? AccountUri { get; set; }

    /// <summary>
    /// Gets or sets the Azure Blob storage connection string, used when
    /// <see cref="Store"/> is <c>azureBlob</c> and <see cref="AccountUri"/> is
    /// not set.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the secret name (Azure Key Vault) holding the connection
    /// string, used when <see cref="Store"/> is <c>azureBlob</c> and neither
    /// <see cref="AccountUri"/> nor <see cref="ConnectionString"/> is set.
    /// </summary>
    public string? ConnectionStringSecretId { get; set; }

    /// <summary>Gets or sets the Azure Blob container name, used when <see cref="Store"/> is <c>azureBlob</c>.</summary>
    public string? ContainerName { get; set; }

    /// <summary>Gets or sets the AWS region (e.g. <c>us-east-1</c>), used when <see cref="Store"/> is <c>s3</c>.</summary>
    public string? Region { get; set; }

    /// <summary>Gets or sets the S3 bucket name, used when <see cref="Store"/> is <c>s3</c>.</summary>
    public string? BucketName { get; set; }

    /// <summary>
    /// Gets or sets the secret id (AWS Secrets Manager) holding the access key,
    /// used when <see cref="Store"/> is <c>s3</c> and explicit credentials are
    /// desired instead of the default credential chain.
    /// </summary>
    public string? AccessKeySecretId { get; set; }

    /// <summary>
    /// Gets or sets the secret id (AWS Secrets Manager) holding the secret access
    /// key, paired with <see cref="AccessKeySecretId"/>.
    /// </summary>
    public string? SecretKeySecretId { get; set; }

    /// <summary>
    /// Gets or sets the optional secret id (AWS Secrets Manager) holding the
    /// session token for temporary credentials.
    /// </summary>
    public string? SessionTokenSecretId { get; set; }

    /// <summary>
    /// Gets or sets the optional IAM role ARN to assume for S3 access, used when
    /// <see cref="Store"/> is <c>s3</c>.
    /// </summary>
    public string? RoleArn { get; set; }

    /// <summary>Gets or sets the optional session name used when assuming <see cref="RoleArn"/>.</summary>
    public string? RoleSessionName { get; set; }
}
