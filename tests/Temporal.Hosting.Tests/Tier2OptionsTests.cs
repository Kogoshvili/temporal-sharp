using Kogoshvili.Temporal.Configuration;
using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Temporalio.Client;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.Hosting.Tests;

public class ConnectionTransportOptionsTests
{
    [Fact]
    public void AddTemporal_Configuration_ConnectionOptions_ReachConnectOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Temporal:TargetHost"] = "host:7233",
                ["Temporal:KeepAlive:Interval"] = "00:00:45",
                ["Temporal:KeepAlive:Timeout"] = "00:00:10",
                ["Temporal:HttpConnectProxy:TargetHost"] = "proxy:8080",
                ["Temporal:HttpConnectProxy:Username"] = "user",
                ["Temporal:HttpConnectProxy:Password"] = "pass",
                ["Temporal:DnsLoadBalancing:ResolutionInterval"] = "00:01:00",
                ["Temporal:GrpcCompression:Mode"] = "none",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTemporal(configuration);

        using var provider = services.BuildServiceProvider();
        var connect = provider.GetRequiredService<IOptions<TemporalClientConnectOptions>>().Value;

        Assert.NotNull(connect.KeepAlive);
        Assert.Equal(TimeSpan.FromSeconds(45), connect.KeepAlive!.Interval);
        Assert.Equal(TimeSpan.FromSeconds(10), connect.KeepAlive!.Timeout);
        Assert.NotNull(connect.HttpConnectProxy);
        Assert.Equal("proxy:8080", connect.HttpConnectProxy!.TargetHost);
        Assert.Equal(("user", "pass"), connect.HttpConnectProxy!.BasicAuth);
        Assert.NotNull(connect.DnsLoadBalancing);
        Assert.Equal(TimeSpan.FromSeconds(60), connect.DnsLoadBalancing!.ResolutionInterval);
        Assert.IsType<GrpcCompression.None>(connect.GrpcCompression);
    }

    [Fact]
    public void AddTemporal_Configuration_ConnectionOptionsNull_LeaveSdkDefaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Temporal:TargetHost"] = "host:7233",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTemporal(configuration);

        using var provider = services.BuildServiceProvider();
        var connect = provider.GetRequiredService<IOptions<TemporalClientConnectOptions>>().Value;

        Assert.NotNull(connect.KeepAlive);
        Assert.Null(connect.HttpConnectProxy);
        Assert.Null(connect.DnsLoadBalancing);
        Assert.IsType<GrpcCompression.Gzip>(connect.GrpcCompression);
    }

    [Fact]
    public void AddTemporal_Configuration_GrpcCompressionInvalid_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Temporal:TargetHost"] = "host:7233",
                ["Temporal:GrpcCompression:Mode"] = "lz4",
            })
            .Build();

        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddTemporal(configuration));
    }
}

public class ActivityOptionsPresetTests
{
    [Fact]
    public void Build_MapsConfiguredValues()
    {
        var preset = new ActivityOptionsPreset
        {
            ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
            HeartbeatTimeout = TimeSpan.FromSeconds(30),
            CancellationType = ActivityCancellationType.WaitCancellationCompleted,
            TaskQueue = "other-queue",
            Retry = new ActivityRetryPolicyOptions
            {
                InitialInterval = TimeSpan.FromSeconds(2),
                MaximumAttempts = 3,
            },
        };

        var options = ActivityOptionsFactory.Build(preset);

        Assert.NotNull(options);
        Assert.Equal(TimeSpan.FromMinutes(5), options!.ScheduleToCloseTimeout);
        Assert.Null(options.StartToCloseTimeout);
        Assert.Null(options.ScheduleToStartTimeout);
        Assert.Equal(TimeSpan.FromSeconds(30), options.HeartbeatTimeout);
        Assert.Equal(ActivityCancellationType.WaitCancellationCompleted, options.CancellationType);
        Assert.Equal("other-queue", options.TaskQueue);
        Assert.NotNull(options.RetryPolicy);
        Assert.Equal(TimeSpan.FromSeconds(2), options.RetryPolicy!.InitialInterval);
        Assert.Equal(3, options.RetryPolicy.MaximumAttempts);
        Assert.Equal(2.0F, options.RetryPolicy.BackoffCoefficient);
    }

    [Fact]
    public void Build_NullPreset_ReturnsNull()
    {
        Assert.Null(ActivityOptionsFactory.Build(null));
    }

    [Fact]
    public void Build_WithoutRetry_LeavesRetryPolicyNull()
    {
        var preset = new ActivityOptionsPreset { StartToCloseTimeout = TimeSpan.FromSeconds(1) };

        var options = ActivityOptionsFactory.Build(preset);

        Assert.NotNull(options);
        Assert.Null(options!.RetryPolicy);
    }

    [Fact]
    public void AddTemporal_Configuration_ActivityOptionsWithoutTimeout_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Temporal:TargetHost"] = "host:7233",
                ["Temporal:ActivityOptions:Presets:broken:HeartbeatTimeout"] = "00:00:30",
            })
            .Build();

        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddTemporal(configuration));
    }

    [Fact]
    public void AddTemporal_Configuration_SeedsRegistryAndBindsOptions()
    {
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Temporal:TargetHost"] = "host:7233",
                    ["Temporal:ActivityOptions:Default:ScheduleToCloseTimeout"] = "00:05:00",
                    ["Temporal:ActivityOptions:Default:HeartbeatTimeout"] = "00:00:30",
                    ["Temporal:ActivityOptions:Presets:long:ScheduleToCloseTimeout"] = "00:30:00",
                })
                .Build();

            var services = new ServiceCollection();
            services.AddTemporal(configuration);

            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<TemporalOptions>>().Value;

            Assert.NotNull(options.ActivityOptions);
            Assert.NotNull(options.ActivityOptions!.Default);
            Assert.Equal(TimeSpan.FromMinutes(5), options.ActivityOptions.Default!.ScheduleToCloseTimeout);

            Assert.Equal(TimeSpan.FromMinutes(5), ActivityOptionsRegistry.GetDefault()!.ScheduleToCloseTimeout);
            Assert.Equal(TimeSpan.FromMinutes(30), ActivityOptionsRegistry.Get("long").ScheduleToCloseTimeout);
            Assert.Contains("long", ActivityOptionsRegistry.Names);
        }
        finally
        {
            ActivityOptionsRegistry.Replace(null, new Dictionary<string, ActivityOptions>());
        }
    }
}

public class ActivityOptionsRegistryTests
{
    [Fact]
    public void Get_And_GetDefault_ResolvePresets()
    {
        try
        {
            var defaultOptions = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(1) };
            var named = new ActivityOptions { ScheduleToCloseTimeout = TimeSpan.FromSeconds(5) };

            ActivityOptionsRegistry.Replace(
                defaultOptions,
                new Dictionary<string, ActivityOptions> { ["long"] = named });

            Assert.Same(defaultOptions, ActivityOptionsRegistry.GetDefault());
            Assert.Same(named, ActivityOptionsRegistry.Get("long"));
            Assert.True(ActivityOptionsRegistry.TryGet("long", out var got));
            Assert.Same(named, got);
            Assert.False(ActivityOptionsRegistry.TryGet("missing", out _));
            Assert.Contains("long", ActivityOptionsRegistry.Names);
            Assert.Throws<KeyNotFoundException>(() => ActivityOptionsRegistry.Get("missing"));
        }
        finally
        {
            ActivityOptionsRegistry.Replace(null, new Dictionary<string, ActivityOptions>());
        }
    }
}

public class TemporalHealthCheckTests
{
    [Fact]
    public void AddTemporalHealthChecks_RegistersCheck()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal();
        services.AddTemporalHealthChecks();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<HealthCheckService>());
        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        Assert.Contains(registrations, r => r.Name == "temporal");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDisabled_ReturnsHealthyWithoutConnecting()
    {
        var services = new ServiceCollection();
        services.Configure<TemporalOptions>(o => o.HealthChecks = new TemporalHealthChecksOptions { Enabled = false });
        services.AddSingleton(new TemporalWorkerTaskQueueRegistry());

        using var provider = services.BuildServiceProvider();
        var check = new TemporalHealthCheck(
            TemporalClient.CreateLazy(new TemporalClientConnectOptions("127.0.0.1:1")),
            provider.GetRequiredService<IOptionsMonitor<TemporalOptions>>(),
            provider.GetRequiredService<TemporalWorkerTaskQueueRegistry>(),
            NullLogger<TemporalHealthCheck>.Instance);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public void AddTemporalWorker_RegistersQueueName()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal();
        services.AddTemporalWorker("queue-a");
        services.AddTemporalWorker("queue-b");

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<TemporalWorkerTaskQueueRegistry>();

        Assert.Equal(2, registry.TaskQueues.Count);
        Assert.Contains("queue-a", registry.TaskQueues);
        Assert.Contains("queue-b", registry.TaskQueues);
    }

    [Fact]
    public void Evaluate_NotServing_Unhealthy()
    {
        var result = TemporalHealthCheck.Evaluate(null, new Dictionary<string, int?>());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public void Evaluate_ServingNoQueues_Healthy()
    {
        var result = TemporalHealthCheck.Evaluate(true, new Dictionary<string, int?>());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public void Evaluate_ServingWithPollers_Healthy()
    {
        var result = TemporalHealthCheck.Evaluate(
            true,
            new Dictionary<string, int?> { ["queue-a"] = 1, ["queue-b"] = 2 });

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public void Evaluate_ServingButZeroPollers_Degraded()
    {
        var result = TemporalHealthCheck.Evaluate(
            true,
            new Dictionary<string, int?> { ["queue-a"] = 1, ["queue-b"] = 0 });

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("queue-b", result.Description);
    }
}
