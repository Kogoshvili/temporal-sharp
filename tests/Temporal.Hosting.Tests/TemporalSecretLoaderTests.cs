using Kogoshvili.Temporal.Codec;
using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Temporalio.Client;

namespace Kogoshvili.Temporal.Hosting.Tests;

public class TemporalSecretLoaderTests
{
    private const string Key = "test-key-test-key-test-key-test!";

    [Fact]
    public async Task StartAsync_VaultEncryption_AppliesResolvedCodecToConnectOptions()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new TemporalClientConnectOptions());
        services.AddOptions<TemporalOptions>().Configure(o => o.DataConverter = new TemporalDataConverterOptions
        {
            Encryption = new TemporalEncryptionCodecOptions
            {
                Enabled = true,
                Source = "azureKeyVault",
                SecretId = "my-key",
                KeyId = "key-1",
            },
        });
        services.AddSingleton<ISecretResolver>(new FakeSecretResolver("azureKeyVault", Key));

        using var provider = services.BuildServiceProvider();
        var loader = CreateLoader(provider);

        await loader.StartAsync(CancellationToken.None);

        var codec = provider.GetRequiredService<TemporalClientConnectOptions>().DataConverter.PayloadCodec;
        var encryption = Assert.IsType<EncryptionCodec>(codec);
        Assert.Equal("key-1", encryption.KeyId);
    }

    [Fact]
    public async Task StartAsync_UnregisteredResolver_Throws()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new TemporalClientConnectOptions());
        services.AddOptions<TemporalOptions>().Configure(o => o.DataConverter = new TemporalDataConverterOptions
        {
            Encryption = new TemporalEncryptionCodecOptions { Enabled = true, Source = "azureKeyVault", SecretId = "my-key" },
        });

        using var provider = services.BuildServiceProvider();
        var loader = CreateLoader(provider);

        await Assert.ThrowsAsync<InvalidOperationException>(() => loader.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task StartAsync_ConfigSource_DoesNothing()
    {
        var services = new ServiceCollection();
        var connectOptions = new TemporalClientConnectOptions();
        services.AddSingleton(connectOptions);
        services.AddOptions<TemporalOptions>().Configure(o => o.DataConverter = new TemporalDataConverterOptions
        {
            Encryption = new TemporalEncryptionCodecOptions { Enabled = true, Source = "config", Key = Key },
        });

        using var provider = services.BuildServiceProvider();
        var loader = CreateLoader(provider);

        await loader.StartAsync(CancellationToken.None);

        Assert.Null(connectOptions.DataConverter.PayloadCodec);
    }

    [Fact]
    public async Task StartAsync_CloudClaimCheck_BuildsStoreViaFactory()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new TemporalClientConnectOptions());
        services.AddOptions<TemporalOptions>().Configure(o => o.DataConverter = new TemporalDataConverterOptions
        {
            ClaimCheck = new TemporalClaimCheckCodecOptions { Enabled = true, Store = "s3", Region = "us-east-1", BucketName = "my-bucket" },
        });
        services.AddSingleton<IClaimCheckStoreFactory>(new FakeStoreFactory());

        using var provider = services.BuildServiceProvider();
        var loader = CreateLoader(provider);

        await loader.StartAsync(CancellationToken.None);

        var codec = provider.GetRequiredService<TemporalClientConnectOptions>().DataConverter.PayloadCodec;
        Assert.IsType<ClaimCheckCodec>(codec);
    }

    private static TemporalSecretLoader CreateLoader(ServiceProvider provider) =>
        new(
            provider.GetRequiredService<TemporalClientConnectOptions>(),
            provider.GetRequiredService<IOptions<TemporalOptions>>(),
            provider.GetRequiredService<IEnumerable<ISecretResolver>>(),
            provider.GetRequiredService<IEnumerable<IClaimCheckStoreFactory>>(),
            NullLogger<TemporalSecretLoader>.Instance);

    private sealed class FakeSecretResolver : ISecretResolver
    {
        private readonly string value;

        public FakeSecretResolver(string name, string value)
        {
            Name = name;
            this.value = value;
        }

        public string Name { get; }

        public Task<string> ResolveAsync(string secretId, CancellationToken cancellationToken = default) =>
            Task.FromResult(value);
    }

    private sealed class FakeStoreFactory : IClaimCheckStoreFactory
    {
        public string Name => "s3";

        public IClaimCheckStore Create(ClaimCheckStoreSettings settings) =>
            new FakeStore();
    }

    private sealed class FakeStore : IClaimCheckStore
    {
        public Task<string> StoreAsync(byte[] data, CancellationToken cancellationToken = default) =>
            Task.FromResult("key");

        public Task<byte[]> LoadAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Empty<byte>());
    }
}
