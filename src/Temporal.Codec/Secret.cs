using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kogoshvili.Temporal.Codec;

/// <summary>
/// A value that is encrypted independently of the payload codec, so it stays
/// unreadable even after the surrounding payload has been decrypted (for example
/// by the Temporal UI's codec server). Combined with the whole-payload
/// <see cref="EncryptionCodec"/> this produces double encryption: the secret's
/// inner value remains ciphertext until explicitly decrypted.
/// </summary>
/// <typeparam name="T">The secret value type, serialized as JSON before encryption.</typeparam>
/// <remarks>
/// The value starts plaintext and is encrypted by the
/// <see cref="SecretEncryptionInterceptor"/> before it crosses the wire. It is
/// intended to be carried opaquely through a workflow (never read there) and
/// decrypted where it is consumed. Once encrypted, serialization produces the
/// same shape the <see cref="EncryptionCodec"/> emits — an <c>encoding</c>, an
/// <c>encryption-key-id</c>, and the ciphertext.
/// </remarks>
[JsonConverter(typeof(SecretJsonConverterFactory))]
public sealed class Secret<T> : ISecret
{
    private T? plaintext;
    private bool hasPlaintext;
    private byte[]? ciphertext;
    private string? keyId;

    /// <summary>
    /// Initializes a new instance of the <see cref="Secret{T}"/> class holding a
    /// plaintext value.
    /// </summary>
    public Secret(T value)
    {
        plaintext = value;
        hasPlaintext = true;
    }

    private Secret(byte[] ciphertext, string keyId)
    {
        this.ciphertext = ciphertext;
        this.keyId = keyId;
        hasPlaintext = false;
    }

    /// <summary>Gets the plaintext value, throwing if the value is still encrypted.</summary>
    public T Value => hasPlaintext
        ? plaintext!
        : throw new InvalidOperationException(
            "The secret is encrypted. Decrypt it first (e.g. via SecretEncryptionInterceptor) before reading its value.");

    /// <inheritdoc />
    public bool IsEncrypted => !hasPlaintext;

    /// <inheritdoc />
    public string KeyId => keyId
        ?? throw new InvalidOperationException("The secret has not been encrypted and has no key id.");

    /// <inheritdoc />
    public Task EncryptAsync(byte[] key, string keyId)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrEmpty(keyId);

        if (IsEncrypted)
        {
            return Task.CompletedTask;
        }

        ciphertext = AesGcmCipher.Encrypt(key, JsonSerializer.SerializeToUtf8Bytes(plaintext));
        this.keyId = keyId;
        plaintext = default;
        hasPlaintext = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DecryptAsync(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!IsEncrypted)
        {
            return Task.CompletedTask;
        }

        plaintext = JsonSerializer.Deserialize<T>(AesGcmCipher.Decrypt(key, ciphertext!));
        ciphertext = null;
        keyId = null;
        hasPlaintext = true;
        return Task.CompletedTask;
    }

    internal byte[] Ciphertext => ciphertext ?? throw new InvalidOperationException("The secret is not encrypted.");

    internal static Secret<T> FromCiphertext(byte[] ciphertext, string keyId) => new(ciphertext, keyId);
}
