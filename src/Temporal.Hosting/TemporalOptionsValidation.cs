namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Shared validation rules for <see cref="TemporalOptions"/>, used both by the
/// eager validation performed in <c>AddTemporal</c> and by the options-pipeline
/// validator that re-checks on every configuration reload.
/// </summary>
internal static class TemporalOptionsValidation
{
    public static void Validate(TemporalOptions options)
    {
        if (options.TestServer.Port < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Temporal:TestServer:Port must be zero or greater.");
        }

        if (options.Tls is { Disabled: true } tls &&
            (tls.Domain is not null ||
             tls.ServerRootCACertPath is not null ||
             tls.ClientCertPath is not null ||
             tls.ClientPrivateKeyPath is not null))
        {
            throw new InvalidOperationException(
                "TLS cannot be disabled while certificate options are configured.");
        }

        if (options.DataConverter.Encryption.Enabled)
        {
            if (string.IsNullOrEmpty(options.DataConverter.Encryption.Key))
            {
                throw new InvalidOperationException(
                    "Temporal:DataConverter:Encryption:Key must be set when encryption is enabled.");
            }

            var keyLength = System.Text.Encoding.ASCII.GetByteCount(options.DataConverter.Encryption.Key);
            if (keyLength is not (16 or 24 or 32))
            {
                throw new InvalidOperationException(
                    "Temporal:DataConverter:Encryption:Key must be 16, 24, or 32 ASCII bytes.");
            }
        }

        if (options.DataConverter.ClaimCheck.Enabled &&
            options.DataConverter.ClaimCheck.ThresholdBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Temporal:DataConverter:ClaimCheck:ThresholdBytes must be zero or greater.");
        }
    }
}
