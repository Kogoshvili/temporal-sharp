using Kogoshvili.Temporal.Codec;

namespace Kogoshvili.Temporal.Codec.Tests;

public class SecretEncryptionInterceptorTests
{
    private sealed class TestResolver(string value) : ISecretResolver
    {
        public string Name => "test";
        public Task<string> ResolveAsync(string secretId, CancellationToken cancellationToken = default) =>
            Task.FromResult(value);
    }

    private class Patient
    {
        public string Name { get; set; } = "";
        public Secret<string> Ssn { get; set; } = new("");
    }

    [Fact]
    public async Task EncryptAsync_EncryptsNestedSecretsInPlace()
    {
        var interceptor = new SecretEncryptionInterceptor(
            new TestResolver("test-key-test-key-test-key-test!"), "secret-id", "vault-key");

        var patient = new Patient { Name = "Alice", Ssn = new Secret<string>("123-45-6789") };
        object?[] args = { patient };

        await interceptor.EncryptAsync(args);

        Assert.True(patient.Ssn.IsEncrypted);
        Assert.Equal("Alice", patient.Name);
    }

    [Fact]
    public async Task EncryptThenDecrypt_RoundTripsNestedSecrets()
    {
        var interceptor = new SecretEncryptionInterceptor(
            new TestResolver("test-key-test-key-test-key-test!"), "secret-id", "vault-key");

        var patient = new Patient { Name = "Alice", Ssn = new Secret<string>("123-45-6789") };
        object?[] args = { patient };

        await interceptor.EncryptAsync(args);
        await interceptor.DecryptAsync(args);

        Assert.Equal("123-45-6789", patient.Ssn.Value);
        Assert.Equal("Alice", patient.Name);
    }

    [Fact]
    public async Task EncryptAsync_ReachesSecretsInCollections()
    {
        var interceptor = new SecretEncryptionInterceptor(
            new TestResolver("test-key-test-key-test-key-test!"), "secret-id", "vault-key");

        var list = new List<Secret<string>> { new("a"), new("b") };
        var dict = new Dictionary<string, Secret<int>> { ["x"] = new(1) };
        object?[] args = { list, dict };

        await interceptor.EncryptAsync(args);

        Assert.All(list, s => Assert.True(s.IsEncrypted));
        Assert.True(dict["x"].IsEncrypted);
    }

    [Fact]
    public async Task EncryptAsync_IgnoresNonSecretValues()
    {
        var interceptor = new SecretEncryptionInterceptor(
            new TestResolver("test-key-test-key-test-key-test!"), "secret-id", "vault-key");

        var value = 42;
        var text = "plain";
        object?[] args = { value, text, null };

        await interceptor.EncryptAsync(args);

        Assert.Equal(42, value);
        Assert.Equal("plain", text);
    }

    [Fact]
    public async Task DecryptAsync_ThrowsOnMismatchedKeyId()
    {
        var interceptor = new SecretEncryptionInterceptor(
            new TestResolver("test-key-test-key-test-key-test!"), "secret-id", "vault-key");

        var secret = new Secret<string>("123-45-6789");
        await secret.EncryptAsync(
            System.Text.Encoding.ASCII.GetBytes("test-key-test-key-test-key-test!"), "other-key");
        object?[] args = { secret };

        await Assert.ThrowsAsync<InvalidOperationException>(() => interceptor.DecryptAsync(args));
    }
}
