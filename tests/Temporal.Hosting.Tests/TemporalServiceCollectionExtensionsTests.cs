using System.Diagnostics.Metrics;
using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Temporalio.Client;
using Temporalio.Extensions.Hosting;

namespace Kogoshvili.Temporal.Hosting.Tests;

public class TemporalServiceCollectionExtensionsTests
{
    [Fact]
    public void AddTemporal_Default_RegistersClientAndOptions()
    {
        var services = new ServiceCollection();
        services.AddTemporal();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<ITemporalClient>());
        Assert.NotNull(provider.GetService<IOptions<TemporalOptions>>());
        Assert.NotNull(provider.GetService<IOptionsMonitor<TemporalOptions>>());
        Assert.Null(provider.GetService<TemporalMetricsInterceptor>());
        Assert.Null(provider.GetService<TemporalTestServerService>());
    }

    [Fact]
    public void AddTemporal_MetricsEnabled_RegistersMeterAndInterceptor()
    {
        var services = new ServiceCollection();
        services.AddTemporal(new TemporalOptions { Metrics = new TemporalMetricsOptions { Enabled = true } });

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<Meter>());
        Assert.NotNull(provider.GetService<TemporalMetricsInterceptor>());
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(Temporalio.Runtime.TemporalRuntime));
    }

    [Fact]
    public void AddTemporal_MetricsWithPrometheus_RegistersRuntime()
    {
        var services = new ServiceCollection();
        services.AddTemporal(new TemporalOptions
        {
            Metrics = new TemporalMetricsOptions
            {
                Enabled = true,
                PrometheusBindAddress = "0.0.0.0:9000",
            },
        });

        Assert.Contains(services, d => d.ServiceType == typeof(Temporalio.Runtime.TemporalRuntime));
    }

    [Fact]
    public void AddTemporal_TestServerEnabled_RegistersHostedService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal(new TemporalOptions { TestServer = new TemporalTestServerOptions { Enabled = true } });

        Assert.Contains(services, d => d.ServiceType == typeof(TemporalTestServerService));
        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService));
        Assert.Contains(services, d => d.ServiceType == typeof(TemporalClientConnectOptions));
    }

    [Fact]
    public void AddTemporal_NegativeTestServerPort_Throws()
    {
        var services = new ServiceCollection();

        var options = new TemporalOptions { TestServer = new TemporalTestServerOptions { Port = -1 } };
        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddTemporal(options));
    }

    [Fact]
    public void AddTemporal_DisabledTlsWithCertificates_Throws()
    {
        var services = new ServiceCollection();

        var options = new TemporalOptions
        {
            Tls = new Kogoshvili.Temporal.Configuration.TemporalTlsOptions
            {
                Disabled = true,
                ClientCertPath = "/path/to/cert.pem",
            },
        };
        Assert.Throws<InvalidOperationException>(() => services.AddTemporal(options));
    }

    [Fact]
    public void AddTemporal_Configuration_BindsNestedOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Temporal:TargetHost"] = "host:7233",
                ["Temporal:Namespace"] = "my-ns",
                ["Temporal:Metrics:Enabled"] = "true",
                ["Temporal:TestServer:Port"] = "1234",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTemporal(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<TemporalOptions>>().Value;

        Assert.Equal("host:7233", options.TargetHost);
        Assert.Equal("my-ns", options.Namespace);
        Assert.True(options.Metrics.Enabled);
        Assert.Equal(1234, options.TestServer.Port);
    }

    [Fact]
    public void AddTemporal_Configuration_LiveReloadsViaOptionsMonitor()
    {
        var path = Path.Combine(Path.GetTempPath(), $"temporal-hosting-{Guid.NewGuid():N}.json");
        WriteJson(path, "host-1:7233");
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile(path, optional: false, reloadOnChange: true)
                .Build();

            var services = new ServiceCollection();
            services.AddTemporal(configuration);

            using var provider = services.BuildServiceProvider();
            var monitor = provider.GetRequiredService<IOptionsMonitor<TemporalOptions>>();

            Assert.Equal("host-1:7233", monitor.CurrentValue.TargetHost);

            WriteJson(path, "host-2:7233");
            configuration.Reload();

            Assert.Equal("host-2:7233", monitor.CurrentValue.TargetHost);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddTemporalWorker_MarkerTypes_AutoDiscoversAndRegistersByLifetime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal();
        services.AddTemporalWorker("queue", typeof(GreetingWorkflow));

        Assert.Contains(services, d => d.ServiceType == typeof(InstanceActivity) && d.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, d => d.ServiceType == typeof(SingletonActivity) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, d => d.ServiceType == typeof(TransientActivity) && d.Lifetime == ServiceLifetime.Transient);
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(StaticActivity));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<TemporalWorkerServiceOptions>>().Get("queue");

        Assert.Contains(options.Workflows, w => w.Type == typeof(GreetingWorkflow));
        Assert.Contains(options.Activities, a => a.Name == "Run");
        Assert.Contains(options.Activities, a => a.Name == "StaticRun");
    }

    [Fact]
    public void AddTemporalWorker_VersioningWithoutVersion_Throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal();

        var deployment = new Temporalio.Worker.WorkerDeploymentOptions();
        Assert.Throws<ArgumentException>(() => services.AddTemporalWorker("queue", deployment));
    }

    private static void WriteJson(string path, string targetHost) =>
        File.WriteAllText(path, $$"""{ "Temporal": { "TargetHost": "{{targetHost}}" } }""");
}

public class TemporalTestServerServiceTests
{
    [Fact]
    public async Task StartAsync_WhenDisabled_DoesNothing()
    {
        var services = new ServiceCollection();
        services.Configure<TemporalOptions>(o => o.TestServer = new TemporalTestServerOptions { Enabled = false });
        services.AddSingleton(new TemporalClientConnectOptions());

        using var provider = services.BuildServiceProvider();
        var service = new TemporalTestServerService(
            provider.GetRequiredService<IOptionsMonitor<TemporalOptions>>(),
            provider.GetRequiredService<TemporalClientConnectOptions>(),
            NullLogger<TemporalTestServerService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
    }
}
