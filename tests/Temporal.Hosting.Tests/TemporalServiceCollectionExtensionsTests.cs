using System.Diagnostics.Metrics;
using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Temporalio.Client;
using Temporalio.Extensions.Hosting;
using Temporalio.Extensions.OpenTelemetry;

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
    public void AddTemporal_MetricsUseDefaultInterceptorFalse_RegistersMeterWithoutInterceptor()
    {
        var services = new ServiceCollection();
        services.AddTemporal(new TemporalOptions
        {
            Metrics = new TemporalMetricsOptions { Enabled = true, UseDefaultInterceptor = false },
        });

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<Meter>());
        Assert.Null(provider.GetService<TemporalMetricsInterceptor>());
        Assert.Null(provider.GetRequiredService<IOptions<TemporalClientConnectOptions>>().Value.Interceptors);
    }

    [Fact]
    public void AddTemporal_TracingEnabled_RegistersTracingInterceptorOnClient()
    {
        var services = new ServiceCollection();
        services.AddTemporal(new TemporalOptions
        {
            Tracing = new TemporalTracingOptions { Enabled = true },
        });

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<BaggageTracingInterceptor>());
        var connect = provider.GetRequiredService<IOptions<TemporalClientConnectOptions>>().Value;
        Assert.NotNull(connect.Interceptors);
        Assert.Contains(connect.Interceptors!, i => i is TracingInterceptor);
    }

    [Fact]
    public void AddTemporal_TracingUseDefaultInterceptorFalse_DoesNotWireInterceptor()
    {
        var services = new ServiceCollection();
        services.AddTemporal(new TemporalOptions
        {
            Tracing = new TemporalTracingOptions { Enabled = true, UseDefaultInterceptor = false },
        });

        using var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<BaggageTracingInterceptor>());
        Assert.Null(provider.GetRequiredService<IOptions<TemporalClientConnectOptions>>().Value.Interceptors);
    }

    [Fact]
    public void AddTemporal_TracingDisabled_RegistersNoTracingInterceptor()
    {
        var services = new ServiceCollection();
        services.AddTemporal();

        using var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<BaggageTracingInterceptor>());
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
    public void AddTemporalWorker_WithoutDiscovery_RegistersNothing()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal();
        services.AddTemporalWorker("queue");

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<TemporalWorkerServiceOptions>>().Get("queue");

        Assert.Empty(options.Workflows);
        Assert.Empty(options.Activities);
    }

    [Fact]
    public void AddTemporalWorker_AddDiscoveredTypes_MarkerType_RegistersByLifetime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal();
        services.AddTemporalWorker("queue").AddDiscoveredTypes(typeof(GreetingWorkflow));

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
    public void AddTemporalWorker_AddDiscoveredTypes_Assembly_RegistersTypes()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal();
        services.AddTemporalWorker("queue").AddDiscoveredTypes(typeof(GreetingWorkflow).Assembly);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<TemporalWorkerServiceOptions>>().Get("queue");

        Assert.Contains(options.Workflows, w => w.Type == typeof(GreetingWorkflow));
        Assert.Contains(options.Activities, a => a.Name == "Run");
    }

    [Fact]
    public void AddTemporalWorker_Tuning_AppliesConfiguredValues()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Temporal:TargetHost"] = "host:7233",
                ["Temporal:Workers:queue:MaxConcurrentActivities"] = "20",
                ["Temporal:Workers:queue:GracefulShutdownTimeout"] = "00:00:30",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTemporal(configuration);
        services.AddTemporalWorker("queue");

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<TemporalWorkerServiceOptions>>().Get("queue");

        Assert.Equal(20, options.MaxConcurrentActivities);
        Assert.Equal(TimeSpan.FromSeconds(30), options.GracefulShutdownTimeout);
    }

    [Fact]
    public void AddTemporalWorker_Tuning_LeavesUnsetOptionsUntouched()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Temporal:TargetHost"] = "host:7233",
                ["Temporal:Workers:queue:MaxConcurrentActivities"] = "20",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTemporal(configuration);
        services.AddTemporalWorker("queue");

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<TemporalWorkerServiceOptions>>().Get("queue");

        Assert.Equal(20, options.MaxConcurrentActivities);
        Assert.Null(options.MaxConcurrentWorkflowTasks);
        Assert.Equal(10000, options.MaxCachedWorkflows);
    }

    [Fact]
    public void AddTemporalWorker_ConfigureOverridesTuning()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Temporal:TargetHost"] = "host:7233",
                ["Temporal:Workers:queue:MaxConcurrentActivities"] = "20",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTemporal(configuration);
        services.AddTemporalWorker("queue", o => o.MaxConcurrentActivities = 5);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<TemporalWorkerServiceOptions>>().Get("queue");

        Assert.Equal(5, options.MaxConcurrentActivities);
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

    [Fact]
    public void AddTemporal_NoTestServer_RegistersConnectionWaiter()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal();

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(TemporalConnectionWaiter));
    }

    [Fact]
    public void AddTemporal_TestServerEnabled_DoesNotRegisterConnectionWaiter()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal(new TemporalOptions { TestServer = new TemporalTestServerOptions { Enabled = true } });

        Assert.DoesNotContain(services, d =>
            d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(TemporalConnectionWaiter));
    }

    [Fact]
    public void AddTemporal_Configuration_RpcRetry_ReachesConnectOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Temporal:TargetHost"] = "host:7233",
                ["Temporal:RpcRetry:MaxRetries"] = "5",
                ["Temporal:RpcRetry:MaxElapsedTime"] = "00:00:30",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTemporal(configuration);

        using var provider = services.BuildServiceProvider();
        var connectOptions = provider.GetRequiredService<IOptions<TemporalClientConnectOptions>>().Value;

        Assert.NotNull(connectOptions.RpcRetry);
        Assert.Equal(5, connectOptions.RpcRetry!.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(30), connectOptions.RpcRetry.MaxElapsedTime);
    }

    [Fact]
    public void AddTemporal_Configuration_InvalidReload_ThrowsOptionsValidationException()
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

            File.WriteAllText(path, """{ "Temporal": { "TestServer": { "Port": -1 } } }""");

            // The invalid value is rejected by the options pipeline on reload.
            // Depending on timing it surfaces synchronously from Reload() (wrapped
            // by the change-token callback) or on the next CurrentValue access.
            var reloadException = Record.Exception(() => configuration.Reload());
            if (reloadException is null)
            {
                Assert.Throws<OptionsValidationException>(() => monitor.CurrentValue);
            }
            else
            {
                Assert.Contains(Flatten(reloadException), e => e is OptionsValidationException);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TemporalOptionsValidator_InvalidPort_ReturnsFailedResult()
    {
        var validator = new TemporalOptionsValidator();

        var result = validator.Validate(
            null,
            new TemporalOptions { TestServer = new TemporalTestServerOptions { Port = -1 } });

        Assert.True(result.Failed);
    }

    [Fact]
    public void AddTemporal_LoggingEnabled_RegistersRuntime()
    {
        var services = new ServiceCollection();
        services.AddTemporal(new TemporalOptions { Logging = new TemporalLoggingOptions { Enabled = true } });

        Assert.Contains(services, d => d.ServiceType == typeof(Temporalio.Runtime.TemporalRuntime));
    }

    [Fact]
    public void AddTemporal_LoggingEnabled_ForwardsCoreLogger()
    {
        var services = new ServiceCollection();
        var factory = new CapturingLoggerFactory();
        services.AddSingleton<ILoggerFactory>(factory);
        services.AddTemporal(new TemporalOptions { Logging = new TemporalLoggingOptions { Enabled = true } });

        using var provider = services.BuildServiceProvider();
        var runtime = provider.GetService<Temporalio.Runtime.TemporalRuntime>();

        Assert.NotNull(runtime);
        Assert.Contains("Temporalio.Core", factory.Categories);
    }

    [Fact]
    public void AddTemporal_LoggingEnabled_NoLoggerFactory_Throws()
    {
        var services = new ServiceCollection();
        services.AddTemporal(new TemporalOptions { Logging = new TemporalLoggingOptions { Enabled = true } });

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetService<Temporalio.Runtime.TemporalRuntime>());
    }

    [Fact]
    public void AddTemporal_LoggingEnabled_SetsClientRuntimeAndLoggerFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal(new TemporalOptions { Logging = new TemporalLoggingOptions { Enabled = true } });

        using var provider = services.BuildServiceProvider();
        var connectOptions = provider.GetRequiredService<IOptions<TemporalClientConnectOptions>>().Value;

        Assert.NotNull(connectOptions.Runtime);
        Assert.NotSame(NullLoggerFactory.Instance, connectOptions.LoggerFactory);
    }

    private static IEnumerable<Exception> Flatten(Exception exception)
    {
        if (exception is AggregateException aggregate)
        {
            return aggregate.Flatten().InnerExceptions.SelectMany(Flatten);
        }

        return new[] { exception };
    }

    private static void WriteJson(string path, string targetHost) =>
        File.WriteAllText(path, $$"""{ "Temporal": { "TargetHost": "{{targetHost}}" } }""");
}

public class TemporalConnectionWaiterTests
{
    [Fact]
    public async Task StartAsync_WhenDisabled_DoesNotConnect()
    {
        var client = CreateLazyClient("127.0.0.1:1");
        var monitor = CreateMonitor(new TemporalConnectionWaitOptions { Enabled = false });

        var waiter = new TemporalConnectionWaiter(monitor, client, NullLogger<TemporalConnectionWaiter>.Instance);

        await waiter.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_WhenTestServerEnabled_DoesNotConnect()
    {
        var client = CreateLazyClient("127.0.0.1:1");
        var monitor = CreateMonitor(
            new TemporalConnectionWaitOptions { Enabled = true },
            testServerEnabled: true);

        var waiter = new TemporalConnectionWaiter(monitor, client, NullLogger<TemporalConnectionWaiter>.Instance);

        await waiter.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_TimesOut_WhenServerUnreachable()
    {
        var client = CreateLazyClient("127.0.0.1:1", failFast: true);
        var monitor = CreateMonitor(new TemporalConnectionWaitOptions
        {
            Enabled = true,
            Timeout = TimeSpan.FromSeconds(1),
            InitialDelay = TimeSpan.FromMilliseconds(50),
            MaxDelay = TimeSpan.FromMilliseconds(50),
        });

        var waiter = new TemporalConnectionWaiter(monitor, client, NullLogger<TemporalConnectionWaiter>.Instance);

        await Assert.ThrowsAnyAsync<Exception>(() => waiter.StartAsync(CancellationToken.None));
    }

    private static ITemporalClient CreateLazyClient(string targetHost, bool failFast = false) =>
        TemporalClient.CreateLazy(new TemporalClientConnectOptions(targetHost)
        {
            Namespace = "default",
            RpcRetry = failFast ? new RpcRetryOptions { MaxRetries = 0 } : null,
        });

    private static IOptionsMonitor<TemporalOptions> CreateMonitor(
        TemporalConnectionWaitOptions connectionWait,
        bool testServerEnabled = false)
    {
        var services = new ServiceCollection();
        services.AddOptions<TemporalOptions>().Configure(options =>
        {
            options.ConnectionWait = connectionWait;
            options.TestServer = new TemporalTestServerOptions { Enabled = testServerEnabled };
        });

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptionsMonitor<TemporalOptions>>();
    }
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

internal sealed class CapturingLoggerFactory : ILoggerFactory, ILoggerProvider, ILogger
{
    public List<string> Categories { get; } = new();

    public ILogger CreateLogger(string categoryName)
    {
        Categories.Add(categoryName);
        return this;
    }

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }

    IDisposable ILogger.BeginScope<TState>(TState state) => NullDisposable.Instance;

    bool ILogger.IsEnabled(LogLevel logLevel) => true;

    void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
