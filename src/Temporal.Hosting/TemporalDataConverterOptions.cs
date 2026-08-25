namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Payload-codec configuration for the starter. The codecs built from this
/// section are composed into a single <c>DataConverter</c> that is applied to
/// the client (and therefore every worker, which inherits the client's
/// converter). Both features are opt-in; when neither is enabled the SDK's
/// default data converter is used unchanged.
/// </summary>
public sealed class TemporalDataConverterOptions
{
    /// <summary>Gets or sets end-to-end payload encryption configuration.</summary>
    public TemporalEncryptionCodecOptions Encryption { get; set; } = new();

    /// <summary>Gets or sets claim-check (large-payload offload) configuration.</summary>
    public TemporalClaimCheckCodecOptions ClaimCheck { get; set; } = new();
}

/// <summary>
/// AES-GCM encryption codec configuration.
/// </summary>
public sealed class TemporalEncryptionCodecOptions
{
    /// <summary>Gets or sets a value indicating whether payload encryption is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the AES-GCM key as an ASCII string (16, 24, or 32 bytes). In
    /// production prefer a key-management system; this string form is provided
    /// for configuration and demo convenience.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>Gets or sets the key id stamped into each payload for key rotation.</summary>
    public string KeyId { get; set; } = "default";
}

/// <summary>
/// Claim-check (large-payload offload) codec configuration. Payloads larger than
/// <see cref="ThresholdBytes"/> are written to a filesystem store and replaced
/// by a reference in the workflow history.
/// </summary>
public sealed class TemporalClaimCheckCodecOptions
{
    /// <summary>Gets or sets a value indicating whether claim-check offloading is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the size threshold in bytes above which payloads are offloaded.
    /// Default is one megabyte.
    /// </summary>
    public int ThresholdBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// Gets or sets the directory the filesystem claim-check store writes blobs
    /// to. Defaults to <c>claim-check</c>.
    /// </summary>
    public string Directory { get; set; } = "claim-check";
}
