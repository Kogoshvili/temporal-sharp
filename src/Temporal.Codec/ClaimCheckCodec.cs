using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;

namespace Kogoshvili.Temporal.Codec;

/// <summary>
/// A <see cref="IPayloadCodec"/> that offloads payloads larger than a threshold
/// to a <see cref="IClaimCheckStore"/>, replacing them with a small reference
/// payload. This keeps the Temporal service's payload size small even for large
/// inputs (and enables the <c>/download</c> external-storage flow).
/// </summary>
/// <remarks>
/// Compose this <em>after</em> the encryption codec in a
/// <see cref="CompositePayloadCodec"/> so the blobs in the store are already
/// ciphertext: <c>serialize → encrypt → claim-check</c>.
/// </remarks>
public sealed class ClaimCheckCodec : IPayloadCodec
{
    private const string Encoding = "binary/claim-check-ref";

    private static readonly ByteString EncodingByteString = ByteString.CopyFromUtf8(Encoding);

    private readonly IClaimCheckStore store;
    private readonly int thresholdBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaimCheckCodec"/> class.
    /// </summary>
    /// <param name="store">The store to offload payloads to.</param>
    /// <param name="thresholdBytes">
    /// Payloads whose serialized size is greater than this are offloaded. Default
    /// is one megabyte.
    /// </param>
    public ClaimCheckCodec(IClaimCheckStore store, int thresholdBytes = 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentOutOfRangeException.ThrowIfNegative(thresholdBytes);
        this.store = store;
        this.thresholdBytes = thresholdBytes;
    }

    /// <summary>Gets the size threshold (in bytes) above which payloads are offloaded.</summary>
    public int ThresholdBytes => thresholdBytes;

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<Payload>> EncodeAsync(IReadOnlyCollection<Payload> payloads)
    {
        var result = new Payload[payloads.Count];
        var index = 0;
        foreach (var payload in payloads)
        {
            var bytes = payload.ToByteArray();
            if (bytes.Length > thresholdBytes)
            {
                var key = await store.StoreAsync(bytes).ConfigureAwait(false);
                result[index++] = new Payload
                {
                    Metadata = { ["encoding"] = EncodingByteString },
                    Data = ByteString.CopyFromUtf8(key),
                };
            }
            else
            {
                result[index++] = payload;
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<Payload>> DecodeAsync(IReadOnlyCollection<Payload> payloads)
    {
        var result = new Payload[payloads.Count];
        var index = 0;
        foreach (var payload in payloads)
        {
            if (payload.Metadata.GetValueOrDefault("encoding") == EncodingByteString)
            {
                var key = payload.Data.ToStringUtf8();
                var bytes = await store.LoadAsync(key).ConfigureAwait(false);
                result[index++] = Payload.Parser.ParseFrom(bytes);
            }
            else
            {
                result[index++] = payload;
            }
        }

        return result;
    }
}
