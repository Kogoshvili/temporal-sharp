using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;

namespace Kogoshvili.Temporal.Codec;

/// <summary>
/// A <see cref="IPayloadCodec"/> that AES-GCM encrypts every payload it is given.
/// The encryption happens after the default payload converter has serialized the
/// value, so the Temporal service only ever sees ciphertext. Compatible with the
/// encryption samples shipped by the other Temporal SDKs (a 12-byte nonce,
/// 16-byte GCM tag, and a key-id in the payload metadata).
/// </summary>
/// <remarks>
/// The key material is supplied at construction. For a demo key the constructor
/// accepts a <see cref="string"/>; prefer the <c>byte[]</c> overload in
/// production and source the key from your key-management infrastructure.
/// </remarks>
public sealed class EncryptionCodec : IPayloadCodec
{
    private static readonly ByteString EncodingByteString = ByteString.CopyFromUtf8("binary/encrypted");

    private readonly byte[] key;
    private readonly string keyId;
    private readonly ByteString keyIdByteString;

    /// <summary>
    /// Initializes a new instance of the <see cref="EncryptionCodec"/> class.
    /// </summary>
    /// <param name="key">The AES-GCM key. Must be 16, 24, or 32 bytes.</param>
    /// <param name="keyId">
    /// An identifier for the key, recorded in each payload's metadata so the
    /// decode side can detect key rotation. Defaults to <c>"default"</c>.
    /// </param>
    public EncryptionCodec(byte[] key, string keyId = "default")
    {
        ArgumentNullException.ThrowIfNull(key);
        this.key = key;
        this.keyId = keyId;
        keyIdByteString = ByteString.CopyFromUtf8(keyId);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EncryptionCodec"/> class from
    /// an ASCII key string. Provided for demo/configuration convenience; the
    /// byte-array overload is preferred for production key material.
    /// </summary>
    public EncryptionCodec(string key, string keyId = "default")
        : this(System.Text.Encoding.ASCII.GetBytes(key), keyId)
    {
    }

    /// <summary>Gets the key id stamped onto encoded payloads.</summary>
    public string KeyId => keyId;

    /// <inheritdoc />
    public Task<IReadOnlyCollection<Payload>> EncodeAsync(IReadOnlyCollection<Payload> payloads) =>
        Task.FromResult<IReadOnlyCollection<Payload>>(payloads.Select(payload => new Payload
        {
            Metadata =
            {
                ["encoding"] = EncodingByteString,
                ["encryption-key-id"] = keyIdByteString,
            },
            Data = ByteString.CopyFrom(AesGcmCipher.Encrypt(key, payload.ToByteArray())),
        }).ToList());

    /// <inheritdoc />
    public Task<IReadOnlyCollection<Payload>> DecodeAsync(IReadOnlyCollection<Payload> payloads) =>
        Task.FromResult<IReadOnlyCollection<Payload>>(payloads.Select(payload =>
        {
            // Only touch payloads this codec encoded; leave everything else alone
            // so unrelated codecs in a chain can process them.
            if (payload.Metadata.GetValueOrDefault("encoding") != EncodingByteString)
            {
                return payload;
            }

            var keyId = payload.Metadata.GetValueOrDefault("encryption-key-id");
            if (keyId != keyIdByteString)
            {
                throw new InvalidOperationException(
                    $"Unrecognized encryption key id '{keyId?.ToStringUtf8()}', expected '{this.keyId}'.");
            }

            return Payload.Parser.ParseFrom(AesGcmCipher.Decrypt(key, payload.Data.ToByteArray()));
        }).ToList());
}
