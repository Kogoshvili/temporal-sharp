namespace Kogoshvili.Temporal.Codec;

/// <summary>
/// A key/value blob store used by <see cref="ClaimCheckCodec"/> to offload
/// payloads that exceed a size threshold. The actual Temporal payload is replaced
/// by a small reference that points at the stored blob.
/// </summary>
/// <remarks>
/// Implementations are expected to be idempotent and to never return the same key
/// for two different blobs. The filesystem implementation ships in this package;
/// Azure Blob and AWS S3 implementations ship in <c>Kogoshvili.Temporal.Cloud</c>.
/// </remarks>
public interface IClaimCheckStore
{
    /// <summary>
    /// Stores the given data and returns the key that can retrieve it later.
    /// </summary>
    Task<string> StoreAsync(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the data previously stored under the given key.
    /// </summary>
    Task<byte[]> LoadAsync(string key, CancellationToken cancellationToken = default);
}
