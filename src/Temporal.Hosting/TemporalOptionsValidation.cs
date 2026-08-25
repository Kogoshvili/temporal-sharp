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
    }
}
