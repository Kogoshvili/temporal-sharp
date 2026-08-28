using System.Text.Json;
using Kogoshvili.Temporal.Codec;

namespace Kogoshvili.Temporal.Codec.Tests;

public class SecretTests
{
    private static readonly byte[] Key = System.Text.Encoding.ASCII.GetBytes("test-key-test-key-test-key-test!");

    [Fact]
    public void Value_Plaintext_ReturnsValue()
    {
        var secret = new Secret<string>("ssn");

        Assert.Equal("ssn", secret.Value);
        Assert.False(secret.IsEncrypted);
    }

    [Fact]
    public async Task EncryptThenDecrypt_RoundTripsValue()
    {
        var secret = new Secret<string>("123-45-6789");

        await secret.EncryptAsync(Key, "vault-key");

        Assert.True(secret.IsEncrypted);
        Assert.Equal("vault-key", secret.KeyId);

        await secret.DecryptAsync(Key);

        Assert.False(secret.IsEncrypted);
        Assert.Equal("123-45-6789", secret.Value);
    }

    [Fact]
    public async Task Value_WhileEncrypted_Throws()
    {
        var secret = new Secret<string>("123-45-6789");
        await secret.EncryptAsync(Key, "vault-key");

        Assert.Throws<InvalidOperationException>(() => secret.Value);
    }

    [Fact]
    public async Task Encrypt_IsIdempotent()
    {
        var secret = new Secret<string>("123-45-6789");
        await secret.EncryptAsync(Key, "vault-key");
        var first = secret.KeyId;

        await secret.EncryptAsync(Key, "vault-key");

        Assert.True(secret.IsEncrypted);
        Assert.Equal(first, secret.KeyId);
    }

    [Fact]
    public async Task Encrypt_SupportsNonStringTypes()
    {
        var secret = new Secret<int>(42);
        await secret.EncryptAsync(Key, "vault-key");
        await secret.DecryptAsync(Key);

        Assert.Equal(42, secret.Value);
    }

    [Fact]
    public async Task Serialize_Encrypted_EmitsEncryptionShape()
    {
        var secret = new Secret<string>("123-45-6789");
        await secret.EncryptAsync(Key, "vault-key");

        var json = JsonSerializer.Serialize(secret);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("binary/encrypted", doc.RootElement.GetProperty("encoding").GetString());
        Assert.Equal("vault-key", doc.RootElement.GetProperty("encryption-key-id").GetString());
        Assert.NotEmpty(doc.RootElement.GetProperty("data").GetString()!);
    }

    [Fact]
    public void Serialize_Plaintext_Throws()
    {
        var secret = new Secret<string>("123-45-6789");

        Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(secret));
    }

    [Fact]
    public async Task Deserialize_ThenDecrypt_RoundTrips()
    {
        var secret = new Secret<string>("123-45-6789");
        await secret.EncryptAsync(Key, "vault-key");
        var json = JsonSerializer.Serialize(secret);

        var restored = JsonSerializer.Deserialize<Secret<string>>(json)!;

        Assert.True(restored.IsEncrypted);
        Assert.Equal("vault-key", restored.KeyId);

        await restored.DecryptAsync(Key);
        Assert.Equal("123-45-6789", restored.Value);
    }
}
