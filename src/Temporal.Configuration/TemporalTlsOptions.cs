namespace Kogoshvili.Temporal.Configuration;

/// <summary>
/// TLS options for the Temporal connection. Certificate properties are file
/// paths that are read at connect time.
/// </summary>
public sealed class TemporalTlsOptions
{
    /// <summary>Gets or sets a value indicating whether TLS is explicitly disabled.</summary>
    public bool Disabled { get; set; }

    /// <summary>Gets or sets the expected server hostname/domain for the certificate.</summary>
    public string? Domain { get; set; }

    /// <summary>Gets or sets the path to the server root CA certificate.</summary>
    public string? ServerRootCACertPath { get; set; }

    /// <summary>Gets or sets the path to the client certificate.</summary>
    public string? ClientCertPath { get; set; }

    /// <summary>Gets or sets the path to the client private key.</summary>
    public string? ClientPrivateKeyPath { get; set; }
}
