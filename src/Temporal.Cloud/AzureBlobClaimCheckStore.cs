using Azure.Storage.Blobs;
using Kogoshvili.Temporal.Codec;

namespace Kogoshvili.Temporal.Cloud;

/// <summary>
/// A <see cref="IClaimCheckStore"/> backed by an Azure Blob Storage container.
/// </summary>
public sealed class AzureBlobClaimCheckStore : IClaimCheckStore
{
    private readonly BlobContainerClient container;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureBlobClaimCheckStore"/> class.
    /// </summary>
    /// <param name="connectionString">The storage account connection string.</param>
    /// <param name="containerName">The container to store blobs in (created if absent).</param>
    public AzureBlobClaimCheckStore(string connectionString, string containerName)
        : this(new BlobContainerClient(connectionString, containerName))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureBlobClaimCheckStore"/> class
    /// from an already-configured container client.
    /// </summary>
    public AzureBlobClaimCheckStore(BlobContainerClient container)
    {
        ArgumentNullException.ThrowIfNull(container);
        this.container = container;
    }

    /// <inheritdoc />
    public async Task<string> StoreAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        var key = $"{Guid.NewGuid():N}";
        await container.UploadBlobAsync(key, new BinaryData(data), cancellationToken).ConfigureAwait(false);
        return key;
    }

    /// <inheritdoc />
    public async Task<byte[]> LoadAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var response = await container.GetBlobClient(key).DownloadContentAsync(cancellationToken).ConfigureAwait(false);
        return response.Value.Content.ToArray();
    }
}
