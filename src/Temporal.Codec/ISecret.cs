namespace Kogoshvili.Temporal.Codec;

/// <summary>
/// Non-generic marker for values that are encrypted independently of the
/// payload codec (per-field encryption). Implemented by <see cref="Secret{T}"/>
/// so the encryption interceptor and object-graph walker can find and transform
/// secrets without reflecting over the generic type argument.
/// </summary>
public interface ISecret
{
    /// <summary>Gets a value indicating whether the value is currently encrypted.</summary>
    bool IsEncrypted { get; }

    /// <summary>Gets the key id stamped onto the value when encrypted.</summary>
    string KeyId { get; }

    /// <summary>Encrypts the value in place with the given key and key id.</summary>
    /// <remarks>Idempotent: encrypting an already-encrypted value is a no-op.</remarks>
    Task EncryptAsync(byte[] key, string keyId);

    /// <summary>Decrypts the value in place with the given key.</summary>
    /// <remarks>Idempotent: decrypting a plaintext value is a no-op.</remarks>
    Task DecryptAsync(byte[] key);
}
