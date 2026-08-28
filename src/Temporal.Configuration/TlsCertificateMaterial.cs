namespace Kogoshvili.Temporal.Configuration;

/// <summary>
/// The resolved PEM certificate material for a TLS connection, independent of
/// where the certificates came from (files, environment variables, or a cloud
/// secret store).
/// </summary>
public sealed record TlsCertificateMaterial(
    byte[]? ServerRootCACert,
    byte[]? ClientCert,
    byte[]? ClientPrivateKey);
