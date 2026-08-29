using Kogoshvili.Temporal.Cli;
using Kogoshvili.Temporal.Configuration;
using Temporalio.Client;

namespace Kogoshvili.Temporal.Cli.Tests;

public class TemporalClientConnectorTests
{
    [Fact]
    public async Task CloudSource_ResolvesMatchingCertificateSource()
    {
        var tls = new TemporalTlsOptions
        {
            Source = "azureKeyVault",
            Domain = "example.tmprl.cloud",
            AzureKeyVault = new AzureKeyVaultTlsOptions
            {
                VaultUri = "https://vault.azure.net",
                CertificateName = "client",
            },
        };
        var material = new TlsCertificateMaterial(
            ServerRootCACert: null,
            ClientCert: new byte[] { 1, 2, 3 },
            ClientPrivateKey: new byte[] { 4, 5, 6 });

        var options = await TemporalClientConnector.ResolveCloudTlsAsync(
            tls,
            new[] { new FakeSource("azureKeyVault", material) },
            CancellationToken.None);

        Assert.Equal("example.tmprl.cloud", options.Domain);
        Assert.Equal(material.ClientCert, options.ClientCert);
        Assert.Equal(material.ClientPrivateKey, options.ClientPrivateKey);
    }

    [Fact]
    public async Task CloudSource_SelectsSourceByName()
    {
        var tls = new TemporalTlsOptions
        {
            Source = "awsSecretsManager",
            AwsSecretsManager = new AwsSecretsManagerTlsOptions
            {
                Region = "us-east-1",
                CertificateSecretId = "cert",
                PrivateKeySecretId = "key",
            },
        };
        var aws = new FakeSource("awsSecretsManager", new TlsCertificateMaterial(null, null, null));
        var azure = new FakeSource("azureKeyVault", new TlsCertificateMaterial(null, null, null));

        var options = await TemporalClientConnector.ResolveCloudTlsAsync(
            tls,
            new[] { azure, aws },
            CancellationToken.None);

        Assert.NotNull(options);
        Assert.Equal(1, aws.ResolveCount);
        Assert.Equal(0, azure.ResolveCount);
    }

    [Fact]
    public async Task CloudSource_UnknownName_Throws()
    {
        var tls = new TemporalTlsOptions { Source = "azureKeyVault" };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TemporalClientConnector.ResolveCloudTlsAsync(
                tls,
                Array.Empty<ITlsCertificateSource>(),
                CancellationToken.None));
    }

    private sealed class FakeSource : ITlsCertificateSource
    {
        private readonly TlsCertificateMaterial material;

        public FakeSource(string name, TlsCertificateMaterial material)
        {
            Name = name;
            this.material = material;
        }

        public string Name { get; }

        public int ResolveCount { get; private set; }

        public Task<TlsCertificateMaterial> ResolveAsync(TemporalTlsOptions options, CancellationToken cancellationToken = default)
        {
            ResolveCount++;
            return Task.FromResult(material);
        }
    }
}
