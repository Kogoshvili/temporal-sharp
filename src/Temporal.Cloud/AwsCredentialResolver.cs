using Amazon.Runtime;
using Amazon.Runtime.Credentials;
using Amazon.S3;

namespace Kogoshvili.Temporal.Cloud;

/// <summary>
/// Resolves AWS credentials using the SDK's default fallback chain (environment
/// variables, the shared credentials/profile files, ECS/EC2 instance roles, and
/// SSO), mirroring how the AWS CLI discovers credentials.
/// </summary>
public static class AwsCredentialResolver
{
    /// <summary>
    /// Resolves credentials from the default fallback chain.
    /// </summary>
    public static AWSCredentials Resolve() =>
        DefaultAWSCredentialsIdentityResolver.GetCredentials(new AmazonS3Config());
}
