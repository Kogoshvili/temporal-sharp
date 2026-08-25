using System.Text;
using Kogoshvili.Temporal.Configuration;
using Temporalio.Client;

namespace Kogoshvili.Temporal.Configuration.Tests;

public class TlsSourceTests
{
    private const string PemCert = "-----BEGIN CERTIFICATE-----\nZm9v\n-----END CERTIFICATE-----\n";
    private const string PemKey = "-----BEGIN PRIVATE KEY-----\nYmFy\n-----END PRIVATE KEY-----\n";

    [Fact]
    public void BuildTls_EnvironmentSource_DecodesRawPem()
    {
        var tls = new TemporalTlsOptions
        {
            Source = "environment",
            ClientCert = PemCert,
            ClientPrivateKey = PemKey,
        };

        var result = ClientOptionsFactory.BuildTls(tls);

        Assert.NotNull(result);
        Assert.Equal(PemCert.Trim(), Encoding.UTF8.GetString(result.ClientCert!));
        Assert.Equal(PemKey.Trim(), Encoding.UTF8.GetString(result.ClientPrivateKey!));
    }

    [Fact]
    public void BuildTls_EnvironmentSource_DecodesBase64()
    {
        var tls = new TemporalTlsOptions
        {
            Source = "environment",
            ClientCert = Convert.ToBase64String(Encoding.UTF8.GetBytes(PemCert)),
        };

        var result = ClientOptionsFactory.BuildTls(tls);

        Assert.Equal(PemCert, Encoding.UTF8.GetString(result!.ClientCert!));
    }

    [Fact]
    public void BuildTls_CloudSource_ReturnsNull()
    {
        var tls = new TemporalTlsOptions
        {
            Source = "azureKeyVault",
            AzureKeyVault = new AzureKeyVaultTlsOptions { VaultUri = "https://v.vault.azure.net", CertificateName = "cert" },
        };

        Assert.Null(ClientOptionsFactory.BuildTls(tls));
    }

    [Fact]
    public void Apply_CloudSource_LeavesTlsNull()
    {
        var connect = new TemporalClientConnectOptions();
        ClientOptionsFactory.Apply(connect, new TemporalConnectionOptions
        {
            Tls = new TemporalTlsOptions
            {
                Source = "azureKeyVault",
                AzureKeyVault = new AzureKeyVaultTlsOptions { VaultUri = "https://v.vault.azure.net", CertificateName = "cert" },
            },
        });

        Assert.Null(connect.Tls);
    }

    [Fact]
    public void Validate_PathAndInlineContent_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new TemporalTlsOptions { ClientCertPath = "a.pem", ClientCert = PemCert }.Validate());
    }

    [Fact]
    public void Validate_UnknownSource_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new TemporalTlsOptions { Source = "bogus" }.Validate());
    }

    [Fact]
    public void Validate_AzureKeyVaultWithoutConfig_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new TemporalTlsOptions { Source = "azureKeyVault" }.Validate());
    }

    [Fact]
    public void Validate_DisabledWithCertificates_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new TemporalTlsOptions { Disabled = true, ClientCertPath = "a.pem" }.Validate());
    }
}
