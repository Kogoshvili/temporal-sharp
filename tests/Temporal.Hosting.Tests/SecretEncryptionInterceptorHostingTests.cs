using Kogoshvili.Temporal.Codec;
using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;

namespace Kogoshvili.Temporal.Hosting.Tests;

public class SecretEncryptionInterceptorHostingTests
{
    private sealed class FakeSecretResolver : ISecretResolver
    {
        public FakeSecretResolver(string name) => Name = name;

        public string Name { get; }

        public Task<string> ResolveAsync(string secretId, CancellationToken cancellationToken = default) =>
            Task.FromResult("test-key-test-key-test-key-test!");
    }

    private static TemporalOptions EnabledOptions() =>
        new()
        {
            DataConverter = new TemporalDataConverterOptions
            {
                Secret = new TemporalSecretEncryptionOptions
                {
                    Enabled = true,
                    Source = "azureKeyVault",
                    SecretId = "my-key",
                    KeyId = "key-1",
                },
            },
        };

    [Fact]
    public void AddTemporal_SecretEnabled_WiresInterceptorOnClient()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISecretResolver>(new FakeSecretResolver("azureKeyVault"));
        services.AddTemporal(EnabledOptions());

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ITemporalClient>();

        Assert.Contains(client.Options.Interceptors!, i => i is SecretEncryptionInterceptor);
    }

    [Fact]
    public void AddTemporal_SecretDisabled_DoesNotWireInterceptor()
    {
        var services = new ServiceCollection();
        services.AddTemporal();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ITemporalClient>();

        Assert.DoesNotContain(client.Options.Interceptors ?? [], i => i is SecretEncryptionInterceptor);
    }

    [Fact]
    public void AddTemporal_SecretEnabled_NoResolver_ThrowsOnResolve()
    {
        var services = new ServiceCollection();
        services.AddTemporal(EnabledOptions());

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<SecretEncryptionInterceptor>());
    }

    [Fact]
    public void AddTemporal_SecretEnabled_UnknownSource_Throws()
    {
        var services = new ServiceCollection();
        var options = EnabledOptions();
        options.DataConverter.Secret.Source = "unknown";

        Assert.Throws<InvalidOperationException>(() => services.AddTemporal(options));
    }

    [Fact]
    public void AddTemporal_SecretEnabled_MissingSecretId_Throws()
    {
        var services = new ServiceCollection();
        var options = EnabledOptions();
        options.DataConverter.Secret.SecretId = null;

        Assert.Throws<InvalidOperationException>(() => services.AddTemporal(options));
    }
}
