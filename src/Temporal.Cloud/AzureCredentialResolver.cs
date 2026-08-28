using Azure.Core;
using Azure.Identity;

namespace Kogoshvili.Temporal.Cloud;

/// <summary>
/// Resolves Azure credentials using the default credential chain
/// (environment variables, managed identity, workload identity, Visual Studio,
/// Azure CLI, and interactive login), so services like Key Vault and Blob
/// Storage authenticate the same way as the rest of the host.
/// </summary>
public static class AzureCredentialResolver
{
    /// <summary>
    /// Creates a <see cref="TokenCredential"/> from the default credential chain.
    /// </summary>
    /// <param name="managedIdentityClientId">
    /// Optional client id of a user-assigned managed identity. When set, only the
    /// managed-identity source is used.
    /// </param>
    public static TokenCredential Resolve(string? managedIdentityClientId = null) =>
        new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ManagedIdentityClientId = managedIdentityClientId,
        });
}
