using Kogoshvili.Temporal.Configuration;
using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Temporalio.Client;

namespace Kogoshvili.Temporal.Hosting.Tests;

public class TemporalCertificateLoaderTests
{
    [Fact]
    public async Task StartAsync_CloudSource_AppliesMaterialToConnectOptions()
    {
        var services = new ServiceCollection();
        services.AddOptions<TemporalClientConnectOptions>();
        services.AddOptions<TemporalOptions>().Configure(o => o.Tls = new TemporalTlsOptions { Source = "test" });
        services.AddSingleton<ITlsCertificateSource>(new FakeSource());

        using var provider = services.BuildServiceProvider();
        var loader = new TemporalCertificateLoader(
            provider.GetRequiredService<IOptions<TemporalClientConnectOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<TemporalOptions>>(),
            provider.GetRequiredService<IEnumerable<ITlsCertificateSource>>(),
            NullLogger<TemporalCertificateLoader>.Instance);

        await loader.StartAsync(CancellationToken.None);

        var tls = provider.GetRequiredService<IOptions<TemporalClientConnectOptions>>().Value.Tls;
        Assert.NotNull(tls);
        Assert.Equal(new byte[] { 1, 2, 3 }, tls!.ClientCert);
        Assert.Equal(new byte[] { 4, 5 }, tls.ClientPrivateKey);
    }

    [Fact]
    public async Task StartAsync_UnregisteredSource_Throws()
    {
        var services = new ServiceCollection();
        services.AddOptions<TemporalClientConnectOptions>();
        services.AddOptions<TemporalOptions>().Configure(o => o.Tls = new TemporalTlsOptions { Source = "test" });

        using var provider = services.BuildServiceProvider();
        var loader = new TemporalCertificateLoader(
            provider.GetRequiredService<IOptions<TemporalClientConnectOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<TemporalOptions>>(),
            provider.GetRequiredService<IEnumerable<ITlsCertificateSource>>(),
            NullLogger<TemporalCertificateLoader>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => loader.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task StartAsync_FileSource_DoesNothing()
    {
        var services = new ServiceCollection();
        services.AddOptions<TemporalClientConnectOptions>();
        services.AddOptions<TemporalOptions>().Configure(o => o.Tls = new TemporalTlsOptions { Source = "file" });

        using var provider = services.BuildServiceProvider();
        var loader = new TemporalCertificateLoader(
            provider.GetRequiredService<IOptions<TemporalClientConnectOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<TemporalOptions>>(),
            provider.GetRequiredService<IEnumerable<ITlsCertificateSource>>(),
            NullLogger<TemporalCertificateLoader>.Instance);

        await loader.StartAsync(CancellationToken.None);

        Assert.Null(provider.GetRequiredService<IOptions<TemporalClientConnectOptions>>().Value.Tls);
    }

    private sealed class FakeSource : ITlsCertificateSource
    {
        public string Name => "test";

        public Task<TlsCertificateMaterial> ResolveAsync(TemporalTlsOptions options, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TlsCertificateMaterial(
                ServerRootCACert: null,
                ClientCert: new byte[] { 1, 2, 3 },
                ClientPrivateKey: new byte[] { 4, 5 }));
    }
}
