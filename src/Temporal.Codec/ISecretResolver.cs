namespace Kogoshvili.Temporal.Codec;

/// <summary>
/// Resolves a secret value (an encryption key, a connection string, an access
/// key, ...) from a particular secret store (Azure Key Vault or AWS Secrets
/// Manager). Implementations are registered in the service container and
/// selected by name.
/// </summary>
/// <remarks>
/// This interface intentionally returns a plain <see cref="string"/> and takes
/// only the secret identifier, so implementations stay free of configuration and
/// hosting types. The vault URI (Azure) or region (AWS) is supplied to the
/// implementation at construction, not here.
/// </remarks>
public interface ISecretResolver
{
    /// <summary>Gets the resolver name used to select the source (e.g. <c>azureKeyVault</c>).</summary>
    string Name { get; }

    /// <summary>Resolves the secret value.</summary>
    /// <param name="secretId">The secret name (Azure Key Vault) or secret id (AWS Secrets Manager).</param>
    Task<string> ResolveAsync(string secretId, CancellationToken cancellationToken = default);
}
