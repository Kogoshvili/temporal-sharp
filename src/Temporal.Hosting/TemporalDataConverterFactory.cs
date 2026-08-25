using Kogoshvili.Temporal.Codec;
using Temporalio.Converters;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Builds the starter's shared <see cref="DataConverter"/> from
/// <see cref="TemporalDataConverterOptions"/>. The resulting converter composes
/// the enabled payload codecs (encryption, then claim-check) and is applied to
/// the client so workers — which inherit the client's converter — use the same
/// encoding.
/// </summary>
public static class TemporalDataConverterFactory
{
    /// <summary>
    /// Builds the payload codec described by the options, or <c>null</c> when no
    /// codec is enabled.
    /// </summary>
    public static IPayloadCodec? BuildCodec(TemporalDataConverterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var codecs = new List<IPayloadCodec>();

        if (options.Encryption.Enabled)
        {
            var key = options.Encryption.Key;
            if (string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException(
                    "Temporal:DataConverter:Encryption:Key must be set when encryption is enabled.");
            }

            codecs.Add(new EncryptionCodec(key, options.Encryption.KeyId));
        }

        if (options.ClaimCheck.Enabled)
        {
            codecs.Add(new ClaimCheckCodec(
                new FileSystemClaimCheckStore(options.ClaimCheck.Directory),
                options.ClaimCheck.ThresholdBytes));
        }

        return codecs.Count switch
        {
            0 => null,
            1 => codecs[0],
            _ => new CompositePayloadCodec(codecs),
        };
    }

    /// <summary>
    /// Builds the data converter described by the options, falling back to the
    /// SDK default when no codec is enabled.
    /// </summary>
    public static DataConverter Build(TemporalDataConverterOptions options)
    {
        var codec = BuildCodec(options);
        return codec is null ? DataConverter.Default : DataConverter.Default with { PayloadCodec = codec };
    }
}
