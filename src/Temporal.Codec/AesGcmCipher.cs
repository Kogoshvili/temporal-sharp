using System.Security.Cryptography;

namespace Kogoshvili.Temporal.Codec;

/// <summary>
/// Shared AES-GCM encrypt/decrypt used by the payload codec and the
/// <c>Secret</c> type, so both emit the same cipher format (a 12-byte nonce,
/// 16-byte GCM tag, and the ciphertext). This mirrors the encryption samples
/// shipped by the other Temporal SDKs.
/// </summary>
internal static class AesGcmCipher
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static byte[] Encrypt(byte[] key, byte[] plaintext)
    {
        var result = new byte[NonceSize + TagSize + plaintext.Length];
        var nonce = result.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, result.AsSpan(NonceSize, plaintext.Length), result.AsSpan(NonceSize + plaintext.Length, TagSize));
        return result;
    }

    public static byte[] Decrypt(byte[] key, byte[] data)
    {
        var result = new byte[data.Length - NonceSize - TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(
            data.AsSpan(0, NonceSize),
            data.AsSpan(NonceSize, result.Length),
            data.AsSpan(NonceSize + result.Length, TagSize),
            result);
        return result;
    }
}
