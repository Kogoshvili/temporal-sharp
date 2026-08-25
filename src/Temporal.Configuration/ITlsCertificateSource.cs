namespace Kogoshvili.Temporal.Configuration;

/// <summary>
/// Resolves the PEM certificate material for a Temporal TLS connection from a
/// particular source (files, environment variables, Azure Key Vault, or AWS
/// Secrets Manager). Implementations are registered in the service container and
/// selected by <see cref="TemporalTlsOptions.Source"/>.
/// </summary>
public interface ITlsCertificateSource
{
    /// <summary>Gets the source name matched against <see cref="TemporalTlsOptions.Source"/>.</summary>
    string Name { get; }

    /// <summary>Resolves the certificate material.</summary>
    Task<TlsCertificateMaterial> ResolveAsync(TemporalTlsOptions options, CancellationToken cancellationToken = default);
}
