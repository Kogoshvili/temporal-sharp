namespace Kogoshvili.Temporal.Configuration;

/// <summary>
/// Resolves TLS certificate material from inline base64/PEM strings
/// (<see cref="TemporalTlsOptions.Source"/> = <c>environment</c>), typically
/// injected as environment variables.
/// </summary>
public sealed class EnvironmentTlsCertificateSource : ITlsCertificateSource
{
    /// <inheritdoc />
    public string Name => "environment";

    /// <inheritdoc />
    public Task<TlsCertificateMaterial> ResolveAsync(TemporalTlsOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Task.FromResult(new TlsCertificateMaterial(
            TlsContent.Decode(options.ServerRootCACert),
            TlsContent.Decode(options.ClientCert),
            TlsContent.Decode(options.ClientPrivateKey)));
    }
}
