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
    /// codec is enabled. Only the synchronous sources are built here (inline
    /// encryption key and the filesystem claim-check store); vault-backed keys
    /// and cloud stores are resolved asynchronously at startup by the secret
    /// loader.
    /// </summary>
    public static IPayloadCodec? BuildCodec(TemporalDataConverterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var codecs = new List<IPayloadCodec>();

        if (options.Encryption.Enabled && options.Encryption.Source == "config")
        {
            codecs.Add(new EncryptionCodec(options.Encryption.Key!, options.Encryption.KeyId));
        }

        if (options.ClaimCheck.Enabled && options.ClaimCheck.Store == "filesystem")
        {
            codecs.Add(new ClaimCheckCodec(
                new FileSystemClaimCheckStore(options.ClaimCheck.Directory),
                options.ClaimCheck.ThresholdBytes));
        }

        return Compose(codecs);
    }

    /// <summary>
    /// Decodes a secret value into AES-GCM key bytes according to the requested
    /// encoding: <c>raw</c> (ASCII), <c>base64</c>, or <c>hex</c>.
    /// </summary>
    public static byte[] DecodeKey(string secret, string encoding)
    {
        ArgumentNullException.ThrowIfNull(secret);

        return encoding switch
        {
            "raw" => System.Text.Encoding.ASCII.GetBytes(secret),
            "base64" => Convert.FromBase64String(secret),
            "hex" => Convert.FromHexString(secret),
            _ => throw new InvalidOperationException(
                $"Unknown key encoding '{encoding}'. Expected 'raw', 'base64', or 'hex'."),
        };
    }

    /// <summary>
    /// Composes a list of payload codecs into a single codec, or <c>null</c> when
    /// the list is empty.
    /// </summary>
    public static IPayloadCodec? Compose(IReadOnlyList<IPayloadCodec> codecs) =>
        codecs.Count switch
        {
            0 => null,
            1 => codecs[0],
            _ => new CompositePayloadCodec(codecs),
        };

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
