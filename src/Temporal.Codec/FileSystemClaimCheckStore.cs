namespace Kogoshvili.Temporal.Codec;

/// <summary>
/// A <see cref="IClaimCheckStore"/> that writes blobs to a local directory, one
/// file per stored payload. Useful for development and for single-node
/// deployments where the codec server and workers share a filesystem.
/// </summary>
public sealed class FileSystemClaimCheckStore : IClaimCheckStore
{
    private readonly string directory;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemClaimCheckStore"/> class.
    /// </summary>
    /// <param name="directory">Directory to store blobs in. Created on first use.</param>
    public FileSystemClaimCheckStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        this.directory = directory;
    }

    /// <summary>Gets the directory blobs are stored in.</summary>
    public string Directory => directory;

    /// <inheritdoc />
    public async Task<string> StoreAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        System.IO.Directory.CreateDirectory(directory);

        var key = $"{Guid.NewGuid():N}";
        await File.WriteAllBytesAsync(Path.Combine(directory, key), data, cancellationToken).ConfigureAwait(false);
        return key;
    }

    /// <inheritdoc />
    public Task<byte[]> LoadAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var fullPath = Path.GetFullPath(Path.Combine(directory, key));
        var rootPath = Path.GetFullPath(directory) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Claim-check key '{key}' is outside the store directory.");
        }

        return File.ReadAllBytesAsync(fullPath, cancellationToken);
    }
}
