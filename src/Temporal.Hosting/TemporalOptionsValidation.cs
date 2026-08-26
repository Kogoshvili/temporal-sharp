using Kogoshvili.Temporal.Configuration;

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

        options.Tls?.Validate();

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

        ValidateActivityOptions(options.ActivityOptions);

        if (options.GrpcCompression is { } compression
            && compression.Mode is not (TemporalGrpcCompressionOptions.Gzip or TemporalGrpcCompressionOptions.None))
        {
            throw new InvalidOperationException(
                $"Temporal:GrpcCompression:Mode must be '{TemporalGrpcCompressionOptions.Gzip}' or '{TemporalGrpcCompressionOptions.None}'.");
        }
    }

    private static void ValidateActivityOptions(TemporalActivityOptions? activityOptions)
    {
        if (activityOptions is null)
        {
            return;
        }

        if (activityOptions.Default is { } defaultPreset)
        {
            ValidatePreset(defaultPreset, "Temporal:ActivityOptions:Default");
        }

        if (activityOptions.Presets is { } presets)
        {
            foreach (var (name, preset) in presets)
            {
                ValidatePreset(preset, $"Temporal:ActivityOptions:Presets:{name}");
            }
        }
    }

    private static void ValidatePreset(ActivityOptionsPreset preset, string path)
    {
        if (preset.ScheduleToCloseTimeout is null && preset.StartToCloseTimeout is null)
        {
            throw new InvalidOperationException(
                $"{path} must set either ScheduleToCloseTimeout or StartToCloseTimeout.");
        }
    }
}
