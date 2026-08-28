using Amazon;
using Amazon.Runtime;
using Kogoshvili.Temporal.Codec;

namespace Kogoshvili.Temporal.Cloud;

/// <summary>
/// Builds an <see cref="S3ClaimCheckStore"/> from resolved settings. Credentials
/// come from an assumed IAM role, explicit access-key secrets, or AWS's default
/// credential chain (which covers instance roles and environment variables), in
/// that order of precedence.
/// </summary>
public sealed class S3ClaimCheckStoreFactory : IClaimCheckStoreFactory
{
    /// <inheritdoc />
    public string Name => "s3";

    /// <inheritdoc />
    public IClaimCheckStore Create(ClaimCheckStoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.Region);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.BucketName);

        var region = RegionEndpoint.GetBySystemName(settings.Region);
        return new S3ClaimCheckStore(BuildCredentials(settings), region, settings.BucketName);
    }

    private static AWSCredentials BuildCredentials(ClaimCheckStoreSettings settings)
    {
        var baseCredentials = settings.AccessKey is not null
            ? BuildBasicCredentials(settings)
            : AwsCredentialResolver.Resolve();

        if (!string.IsNullOrWhiteSpace(settings.RoleArn))
        {
            return new AssumeRoleAWSCredentials(
                baseCredentials,
                settings.RoleArn,
                string.IsNullOrWhiteSpace(settings.RoleSessionName) ? $"temporal-{Guid.NewGuid():N}" : settings.RoleSessionName);
        }

        return baseCredentials;
    }

    private static AWSCredentials BuildBasicCredentials(ClaimCheckStoreSettings settings) =>
        string.IsNullOrWhiteSpace(settings.SessionToken)
            ? new BasicAWSCredentials(settings.AccessKey!, settings.SecretKey)
            : new SessionAWSCredentials(settings.AccessKey!, settings.SecretKey, settings.SessionToken);
}
