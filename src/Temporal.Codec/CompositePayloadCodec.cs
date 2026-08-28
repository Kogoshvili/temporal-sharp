using Temporalio.Api.Common.V1;
using Temporalio.Converters;

namespace Kogoshvili.Temporal.Codec;

/// <summary>
/// Chains several <see cref="IPayloadCodec"/>s into one. Encoding applies the
/// codecs in the order they are given; decoding applies them in reverse order,
/// mirroring the way the SDK composes converters and codecs.
/// </summary>
/// <remarks>
/// For example, <c>new CompositePayloadCodec(encryption, claimCheck)</c> produces
/// <c>serialize → encrypt → offload</c> on encode and
/// <c>fetch → decrypt → deserialize</c> on decode.
/// </remarks>
public sealed class CompositePayloadCodec : IPayloadCodec
{
    private readonly IReadOnlyList<IPayloadCodec> codecs;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositePayloadCodec"/> class.
    /// </summary>
    public CompositePayloadCodec(params IPayloadCodec[] codecs)
        : this((IEnumerable<IPayloadCodec>)codecs)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositePayloadCodec"/> class.
    /// </summary>
    public CompositePayloadCodec(IEnumerable<IPayloadCodec> codecs)
    {
        ArgumentNullException.ThrowIfNull(codecs);
        this.codecs = codecs.ToArray();
        if (this.codecs.Count == 0)
        {
            throw new ArgumentException("At least one codec is required.", nameof(codecs));
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<Payload>> EncodeAsync(IReadOnlyCollection<Payload> payloads)
    {
        foreach (var codec in codecs)
        {
            payloads = await codec.EncodeAsync(payloads).ConfigureAwait(false);
        }

        return payloads;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<Payload>> DecodeAsync(IReadOnlyCollection<Payload> payloads)
    {
        for (var i = codecs.Count - 1; i >= 0; i--)
        {
            payloads = await codecs[i].DecodeAsync(payloads).ConfigureAwait(false);
        }

        return payloads;
    }
}
