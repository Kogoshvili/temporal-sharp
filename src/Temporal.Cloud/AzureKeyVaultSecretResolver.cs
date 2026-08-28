using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using Kogoshvili.Temporal.Codec;

namespace Kogoshvili.Temporal.Cloud;

/// <summary>
/// Resolves secrets from an Azure Key Vault using a <see cref="TokenCredential"/>
/// (managed identity, workload identity, or CLI). The vault URI is fixed at
/// construction; each call resolves a single secret by name.
/// </summary>
public sealed class AzureKeyVaultSecretResolver : ISecretResolver
{
    private readonly SecretClient client;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureKeyVaultSecretResolver"/> class.
    /// </summary>
    /// <param name="vaultUri">The vault URI (e.g. <c>https://my-vault.vault.azure.net</c>).</param>
    /// <param name="credential">The credential used to authenticate to the vault.</param>
    public AzureKeyVaultSecretResolver(string vaultUri, TokenCredential credential)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultUri);
        ArgumentNullException.ThrowIfNull(credential);
        client = new SecretClient(new Uri(vaultUri), credential);
    }

    /// <inheritdoc />
    public string Name => "azureKeyVault";

    /// <inheritdoc />
    public async Task<string> ResolveAsync(string secretId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretId);
        var response = await client.GetSecretAsync(secretId, cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.Value.Value;
    }
}
