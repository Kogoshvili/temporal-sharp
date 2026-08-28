namespace Kogoshvili.Temporal.CodecServer;

/// <summary>
/// Configuration for the codec server endpoints. CORS origins default to the
/// Temporal Cloud UI plus the common local dev servers; the auth modes map
/// directly onto the two options the Temporal Web UI offers for a codec server.
/// </summary>
public sealed class TemporalCodecServerOptions
{
    /// <summary>Gets or sets the authentication configuration.</summary>
    public TemporalCodecServerAuthOptions Auth { get; set; } = new();

    /// <summary>
    /// Gets or sets the origins allowed to call the codec server from the
    /// browser. Must be an explicit list (no wildcard) when
    /// <see cref="AllowCredentials"/> is <c>true</c>.
    /// </summary>
    public string[] AllowedOrigins { get; set; } =
    {
        "https://cloud.temporal.io",
        "http://localhost:8080",
        "http://localhost:8233",
    };

    /// <summary>
    /// Gets or sets a value indicating whether CORS should send credentials
    /// (cookies). Required for the OAuth2 authorization-code
    /// (cross-origin-credentials) mode.
    /// </summary>
    public bool AllowCredentials { get; set; } = true;

    /// <summary>Gets or sets the path that initiates the OAuth2 login flow.</summary>
    public string LoginPath { get; set; } = "/codec/login";

    /// <summary>Gets or sets the path that signs the user out.</summary>
    public string LogoutPath { get; set; } = "/codec/logout";
}

/// <summary>
/// Authentication options for the codec server, mirroring the Temporal Web UI's
/// "Pass access token" and "Include cross-origin credentials" settings.
/// </summary>
public sealed class TemporalCodecServerAuthOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to validate the JWT access token
    /// the Temporal Web UI forwards in the <c>Authorization</c> header.
    /// </summary>
    public bool PassAccessToken { get; set; }

    /// <summary>
    /// Gets or sets the OIDC authority used to validate the access token. Defaults
    /// to Temporal Cloud's login authority.
    /// </summary>
    public string? Authority { get; set; } = "https://login.tmprl.cloud";

    /// <summary>
    /// Gets or sets the expected <c>aud</c> claim. Defaults to Temporal Cloud's
    /// API audience.
    /// </summary>
    public string? Audience { get; set; } = "https://saas-api.tmprl.cloud";

    /// <summary>Gets or sets a value indicating whether the authority must be HTTPS.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to accept browser cookies via an
    /// OAuth2 authorization-code flow, so the codec server maintains its own
    /// session (the "Include cross-origin credentials" option).
    /// </summary>
    public bool IncludeCrossOriginCredentials { get; set; }

    /// <summary>Gets or sets the OIDC authority used by the authorization-code flow.</summary>
    public string? OidcAuthority { get; set; }

    /// <summary>Gets or sets the OIDC client id used by the authorization-code flow.</summary>
    public string? ClientId { get; set; }

    /// <summary>Gets or sets the OIDC client secret used by the authorization-code flow.</summary>
    public string? ClientSecret { get; set; }
}
