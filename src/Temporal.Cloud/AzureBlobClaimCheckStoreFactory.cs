using Kogoshvili.Temporal.Codec;

namespace Kogoshvili.Temporal.Cloud;

/// <summary>
/// Builds an <see cref="AzureBlobClaimCheckStore"/> from resolved settings.
/// Authenticates via managed identity when an account URI is supplied, or via a
/// connection string otherwise.
/// </summary>
public sealed class AzureBlobClaimCheckStoreFactory : IClaimCheckStoreFactory
{
    /// <inheritdoc />
    public string Name => "azureBlob";

    /// <inheritdoc />
    public IClaimCheckStore Create(ClaimCheckStoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.ContainerName);

        if (!string.IsNullOrWhiteSpace(settings.AccountUri))
        {
            return new AzureBlobClaimCheckStore(
                new Uri(settings.AccountUri),
                AzureCredentialResolver.Resolve(),
                settings.ContainerName);
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(settings.ConnectionString);
        return new AzureBlobClaimCheckStore(settings.ConnectionString, settings.ContainerName);
    }
}
