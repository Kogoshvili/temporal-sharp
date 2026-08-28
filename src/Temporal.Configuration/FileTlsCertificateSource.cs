namespace Kogoshvili.Temporal.Configuration;

/// <summary>
/// Resolves TLS certificate material from PEM files on disk
/// (<see cref="TemporalTlsOptions.Source"/> = <c>file</c>).
/// </summary>
public sealed class FileTlsCertificateSource : ITlsCertificateSource
{
    /// <inheritdoc />
    public string Name => "file";

    /// <inheritdoc />
    public Task<TlsCertificateMaterial> ResolveAsync(TemporalTlsOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Task.FromResult(new TlsCertificateMaterial(
            ReadAllBytes(options.ServerRootCACertPath),
            ReadAllBytes(options.ClientCertPath),
            ReadAllBytes(options.ClientPrivateKeyPath)));
    }

    private static byte[]? ReadAllBytes(string? path) =>
        path is null ? null : File.ReadAllBytes(path);
}
