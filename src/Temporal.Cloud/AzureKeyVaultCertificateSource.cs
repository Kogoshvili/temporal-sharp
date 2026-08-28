using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using Kogoshvili.Temporal.Configuration;

namespace Kogoshvili.Temporal.Cloud;

/// <summary>
/// Resolves TLS certificate material from an Azure Key Vault certificate. Key
/// Vault stores certificates as PFX secrets, so this source fetches the PFX and
/// converts it to the PEM form the Temporal SDK requires.
/// </summary>
public sealed class AzureKeyVaultCertificateSource : ITlsCertificateSource
{
    private readonly TokenCredential credential;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureKeyVaultCertificateSource"/> class.
    /// </summary>
    public AzureKeyVaultCertificateSource(TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        this.credential = credential;
    }

    /// <inheritdoc />
    public string Name => "azureKeyVault";

    /// <inheritdoc />
    public async Task<TlsCertificateMaterial> ResolveAsync(TemporalTlsOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var azure = options.AzureKeyVault
            ?? throw new InvalidOperationException("Temporal:Tls:AzureKeyVault must be configured when using the 'azureKeyVault' TLS source.");

        var client = new SecretClient(new Uri(azure.VaultUri!), credential);
        var response = await client.GetSecretAsync(azure.CertificateName, cancellationToken: cancellationToken).ConfigureAwait(false);
        var pfx = Convert.FromBase64String(response.Value.Value);
        return PfxToPem(pfx, azure.Password);
    }

    /// <summary>
    /// Converts a PKCS#12 (PFX) certificate to PEM material. Returns the client
    /// certificate and private key; the server root CA is left null (Key Vault
    /// does not store the Temporal Cloud CA, which is publicly distributed).
    /// </summary>
    public static TlsCertificateMaterial PfxToPem(byte[] pfx, string? password = null)
    {
        ArgumentNullException.ThrowIfNull(pfx);

        using var certificate = new X509Certificate2(pfx, password, X509KeyStorageFlags.Exportable);

        return PfxToPem(certificate);
    }

    /// <summary>
    /// Converts a certificate (with an exportable private key) to PEM material.
    /// </summary>
    public static TlsCertificateMaterial PfxToPem(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        var clientCert = TlsContent.EncodePem(certificate.Export(X509ContentType.Cert), "CERTIFICATE");

        var clientPrivateKey = certificate.GetRSAPrivateKey() is { } rsa
            ? TlsContent.EncodePem(rsa.ExportPkcs8PrivateKey(), "PRIVATE KEY")
            : certificate.GetECDsaPrivateKey() is { } ecdsa
                ? TlsContent.EncodePem(ecdsa.ExportPkcs8PrivateKey(), "PRIVATE KEY")
                : throw new InvalidOperationException("The certificate does not have an exportable RSA or ECDSA private key.");

        return new TlsCertificateMaterial(ServerRootCACert: null, ClientCert: clientCert, ClientPrivateKey: clientPrivateKey);
    }
}
