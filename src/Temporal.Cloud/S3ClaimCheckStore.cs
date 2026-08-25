using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Kogoshvili.Temporal.Codec;

namespace Kogoshvili.Temporal.Cloud;

/// <summary>
/// A <see cref="IClaimCheckStore"/> backed by an Amazon S3 bucket.
/// </summary>
public sealed class S3ClaimCheckStore : IClaimCheckStore
{
    private readonly IAmazonS3 s3;
    private readonly string bucketName;

    /// <summary>
    /// Initializes a new instance of the <see cref="S3ClaimCheckStore"/> class.
    /// </summary>
    /// <param name="credentials">The AWS credentials to use.</param>
    /// <param name="region">The S3 region endpoint.</param>
    /// <param name="bucketName">The bucket to store objects in.</param>
    public S3ClaimCheckStore(AWSCredentials credentials, RegionEndpoint region, string bucketName)
        : this(new AmazonS3Client(credentials, region), bucketName)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="S3ClaimCheckStore"/> class
    /// from an already-configured client.
    /// </summary>
    public S3ClaimCheckStore(IAmazonS3 s3, string bucketName)
    {
        ArgumentNullException.ThrowIfNull(s3);
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        this.s3 = s3;
        this.bucketName = bucketName;
    }

    /// <inheritdoc />
    public async Task<string> StoreAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        var key = $"{Guid.NewGuid():N}";
        using var stream = new MemoryStream(data);
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            InputStream = stream,
        }, cancellationToken).ConfigureAwait(false);
        return key;
    }

    /// <inheritdoc />
    public async Task<byte[]> LoadAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        using var response = await s3.GetObjectAsync(bucketName, key, cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        await response.ResponseStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }
}
