namespace Kogoshvili.Temporal.Codec;

/// <summary>
/// Settings describing the claim-check store a host wants to build, independent
/// of any cloud SDK. Credential values are resolved beforehand (inline, via a
/// secret resolver, or by a cloud credential chain), so this record carries only
/// plain strings. Cloud-backed factories interpret the fields relevant to their
/// store and ignore the rest.
/// </summary>
public sealed record ClaimCheckStoreSettings
{
    /// <summary>Gets or sets the filesystem directory (used by the <c>filesystem</c> store).</summary>
    public string? Directory { get; init; }

    /// <summary>Gets or sets the Azure Blob storage account URI (managed-identity auth).</summary>
    public string? AccountUri { get; init; }

    /// <summary>Gets or sets the Azure Blob storage connection string.</summary>
    public string? ConnectionString { get; init; }

    /// <summary>Gets or sets the Azure Blob container name.</summary>
    public string? ContainerName { get; init; }

    /// <summary>Gets or sets the AWS region (e.g. <c>us-east-1</c>).</summary>
    public string? Region { get; init; }

    /// <summary>Gets or sets the S3 bucket name.</summary>
    public string? BucketName { get; init; }

    /// <summary>Gets or sets the explicit AWS access key id.</summary>
    public string? AccessKey { get; init; }

    /// <summary>Gets or sets the explicit AWS secret access key.</summary>
    public string? SecretKey { get; init; }

    /// <summary>Gets or sets the optional AWS session token (temporary credentials).</summary>
    public string? SessionToken { get; init; }

    /// <summary>Gets or sets the optional IAM role ARN to assume.</summary>
    public string? RoleArn { get; init; }

    /// <summary>Gets or sets the optional session name used when assuming the role.</summary>
    public string? RoleSessionName { get; init; }
}
