using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Kogoshvili.Temporal.Cloud;

namespace Kogoshvili.Temporal.Cloud.Tests;

public class AzureKeyVaultCertificateSourceTests
{
    [Fact]
    public void PfxToPem_ConvertsPfxToPemCertAndKey()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=temporal-test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        var pfx = certificate.Export(X509ContentType.Pfx);

        var material = AzureKeyVaultCertificateSource.PfxToPem(pfx);

        var certPem = Encoding.UTF8.GetString(material.ClientCert!);
        Assert.StartsWith("-----BEGIN CERTIFICATE-----", certPem);
        using var loadedCert = X509Certificate2.CreateFromPem(certPem);
        Assert.Equal("CN=temporal-test", loadedCert.Subject);

        var keyPem = Encoding.UTF8.GetString(material.ClientPrivateKey!);
        Assert.StartsWith("-----BEGIN PRIVATE KEY-----", keyPem);
        using var loadedKey = RSA.Create();
        loadedKey.ImportFromPem(keyPem);
        Assert.Equal(rsa.ExportPkcs8PrivateKey(), loadedKey.ExportPkcs8PrivateKey());
    }
}
