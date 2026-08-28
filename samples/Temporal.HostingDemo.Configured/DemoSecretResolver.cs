using Kogoshvili.Temporal.Codec;

namespace Kogoshvili.Temporal.HostingDemo.Configured;

/// <summary>
/// Stands in for <c>Kogoshvili.Temporal.Cloud.AddAzureKeyVaultSecretResolver</c>
/// so the per-field <c>Secret&lt;T&gt;</c> demo runs without a real vault. It is
/// selected by <c>Temporal:DataConverter:Secret:Source</c> = <c>azureKeyVault</c>.
/// </summary>
public sealed class DemoSecretResolver : ISecretResolver
{
    public string Name => "azureKeyVault";

    public Task<string> ResolveAsync(string secretId, CancellationToken cancellationToken = default) =>
        Task.FromResult("test-key-test-key-test-key-test!");
}
