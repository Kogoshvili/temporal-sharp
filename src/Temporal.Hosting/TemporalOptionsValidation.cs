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

        ValidateEncryption(options.DataConverter.Encryption);
        ValidateClaimCheck(options.DataConverter.ClaimCheck);
        ValidateSecret(options.DataConverter.Secret);

        ValidateActivityOptions(options.ActivityOptions);

        ValidateSearchAttributes(options.SearchAttributes);

        if (options.GrpcCompression is { } compression
            && compression.Mode is not (TemporalGrpcCompressionOptions.Gzip or TemporalGrpcCompressionOptions.None))
        {
            throw new InvalidOperationException(
                $"Temporal:GrpcCompression:Mode must be '{TemporalGrpcCompressionOptions.Gzip}' or '{TemporalGrpcCompressionOptions.None}'.");
        }
    }

    private static void ValidateEncryption(TemporalEncryptionCodecOptions encryption)
    {
        if (!encryption.Enabled)
        {
            return;
        }

        switch (encryption.Source)
        {
            case "config":
                if (string.IsNullOrEmpty(encryption.Key))
                {
                    throw new InvalidOperationException(
                        "Temporal:DataConverter:Encryption:Key must be set when encryption is enabled and Source is 'config'.");
                }

                var keyLength = System.Text.Encoding.ASCII.GetByteCount(encryption.Key);
                if (keyLength is not (16 or 24 or 32))
                {
                    throw new InvalidOperationException(
                        "Temporal:DataConverter:Encryption:Key must be 16, 24, or 32 ASCII bytes.");
                }

                break;

            case "azureKeyVault":
            case "awsSecretsManager":
                if (string.IsNullOrWhiteSpace(encryption.SecretId))
                {
                    throw new InvalidOperationException(
                        $"Temporal:DataConverter:Encryption:SecretId must be set when encryption is enabled and Source is '{encryption.Source}'.");
                }

                if (encryption.Encoding is not ("raw" or "base64" or "hex"))
                {
                    throw new InvalidOperationException(
                        $"Temporal:DataConverter:Encryption:Encoding must be 'raw', 'base64', or 'hex' when encryption is enabled and Source is '{encryption.Source}'.");
                }

                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown Temporal:DataConverter:Encryption:Source '{encryption.Source}'. Expected 'config', 'azureKeyVault', or 'awsSecretsManager'.");
        }
    }

    private static void ValidateClaimCheck(TemporalClaimCheckCodecOptions claimCheck)
    {
        if (!claimCheck.Enabled)
        {
            return;
        }

        if (claimCheck.ThresholdBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(claimCheck),
                "Temporal:DataConverter:ClaimCheck:ThresholdBytes must be zero or greater.");
        }

        switch (claimCheck.Store)
        {
            case "filesystem":
                break;

            case "azureBlob":
                if (string.IsNullOrWhiteSpace(claimCheck.ContainerName))
                {
                    throw new InvalidOperationException(
                        "Temporal:DataConverter:ClaimCheck:ContainerName must be set when Store is 'azureBlob'.");
                }

                break;

            case "s3":
                if (string.IsNullOrWhiteSpace(claimCheck.Region) || string.IsNullOrWhiteSpace(claimCheck.BucketName))
                {
                    throw new InvalidOperationException(
                        "Temporal:DataConverter:ClaimCheck:Region and BucketName must be set when Store is 's3'.");
                }

                if ((claimCheck.AccessKeySecretId is null) != (claimCheck.SecretKeySecretId is null))
                {
                    throw new InvalidOperationException(
                        "Temporal:DataConverter:ClaimCheck:AccessKeySecretId and SecretKeySecretId must be set together when Store is 's3'.");
                }

                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown Temporal:DataConverter:ClaimCheck:Store '{claimCheck.Store}'. Expected 'filesystem', 'azureBlob', or 's3'.");
        }
    }

    private static void ValidateSecret(TemporalSecretEncryptionOptions secret)
    {
        if (!secret.Enabled)
        {
            return;
        }

        if (secret.Source is not ("azureKeyVault" or "awsSecretsManager"))
        {
            throw new InvalidOperationException(
                $"Unknown Temporal:DataConverter:Secret:Source '{secret.Source}'. Expected 'azureKeyVault' or 'awsSecretsManager'.");
        }

        if (string.IsNullOrWhiteSpace(secret.SecretId))
        {
            throw new InvalidOperationException(
                $"Temporal:DataConverter:Secret:SecretId must be set when secret encryption is enabled and Source is '{secret.Source}'.");
        }

        if (secret.Encoding is not ("raw" or "base64" or "hex"))
        {
            throw new InvalidOperationException(
                $"Temporal:DataConverter:Secret:Encoding must be 'raw', 'base64', or 'hex' when secret encryption is enabled and Source is '{secret.Source}'.");
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

        if (activityOptions.LocalDefault is { } localDefaultPreset)
        {
            ValidatePreset(localDefaultPreset, "Temporal:ActivityOptions:LocalDefault");
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

    private static void ValidateSearchAttributes(TemporalSearchAttributesOptions? searchAttributes)
    {
        if (searchAttributes?.Attributes is not { } attributes)
        {
            return;
        }

        foreach (var (name, attribute) in attributes)
        {
            if (attribute.Type == Temporalio.Api.Enums.V1.IndexedValueType.Unspecified)
            {
                throw new InvalidOperationException(
                    $"Temporal:SearchAttributes:Attributes:{name}:Type must be a valid search-attribute type (not Unspecified).");
            }
        }
    }
}
