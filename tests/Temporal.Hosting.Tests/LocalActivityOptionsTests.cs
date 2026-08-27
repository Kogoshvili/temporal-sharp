using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.Hosting.Tests;

[Collection("ActivityOptionsRegistry")]
public class LocalActivityOptionsFactoryTests
{
    [Fact]
    public void Build_MapsSharedAndLocalValues()
    {
        var preset = new ActivityOptionsPreset
        {
            ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
            HeartbeatTimeout = TimeSpan.FromSeconds(30),
            TaskQueue = "other-queue",
            CancellationType = ActivityCancellationType.WaitCancellationCompleted,
            LocalRetryThreshold = TimeSpan.FromSeconds(15),
            ActivityId = "my-activity",
            Summary = "does a thing",
            Retry = new RetryPolicyOptions
            {
                InitialInterval = TimeSpan.FromSeconds(2),
                MaximumAttempts = 3,
            },
        };

        var options = LocalActivityOptionsFactory.Build(preset);

        Assert.NotNull(options);
        Assert.Equal(TimeSpan.FromMinutes(5), options!.ScheduleToCloseTimeout);
        Assert.Null(options.StartToCloseTimeout);
        Assert.Null(options.ScheduleToStartTimeout);
        Assert.Equal(ActivityCancellationType.WaitCancellationCompleted, options.CancellationType);
        Assert.Equal(TimeSpan.FromSeconds(15), options.LocalRetryThreshold);
        Assert.Equal("my-activity", options.ActivityId);
        Assert.Equal("does a thing", options.Summary);
        Assert.NotNull(options.RetryPolicy);
        Assert.Equal(TimeSpan.FromSeconds(2), options.RetryPolicy!.InitialInterval);
        Assert.Equal(3, options.RetryPolicy.MaximumAttempts);
    }

    [Fact]
    public void Build_NullPreset_ReturnsNull()
    {
        Assert.Null(LocalActivityOptionsFactory.Build(null));
    }

    [Fact]
    public void Build_WithoutRetry_LeavesRetryPolicyNull()
    {
        var preset = new ActivityOptionsPreset { StartToCloseTimeout = TimeSpan.FromSeconds(1) };

        var options = LocalActivityOptionsFactory.Build(preset);

        Assert.NotNull(options);
        Assert.Null(options!.RetryPolicy);
    }
}

[Collection("ActivityOptionsRegistry")]
public class ActivityOptionsRegistryLocalTests
{
    [Fact]
    public void GetLocal_ReturnsClone_SoMutationIsIsolated()
    {
        try
        {
            var localDefault = new LocalActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(1) };
            var localNamed = new LocalActivityOptions { ScheduleToCloseTimeout = TimeSpan.FromSeconds(5) };

            ActivityOptionsRegistry.Replace(
                null, new Dictionary<string, ActivityOptions>(),
                localDefault, new Dictionary<string, LocalActivityOptions> { ["long"] = localNamed });

            var defaultClone = ActivityOptionsRegistry.GetLocalDefault();
            var namedClone = ActivityOptionsRegistry.GetLocal("long");

            Assert.NotSame(localDefault, defaultClone);
            Assert.NotSame(localNamed, namedClone);
            Assert.Equal(TimeSpan.FromSeconds(1), defaultClone.StartToCloseTimeout);
            Assert.Equal(TimeSpan.FromSeconds(5), namedClone.ScheduleToCloseTimeout);

            defaultClone.StartToCloseTimeout = TimeSpan.FromSeconds(99);

            Assert.Equal(TimeSpan.FromSeconds(1), ActivityOptionsRegistry.GetLocalDefault().StartToCloseTimeout);
        }
        finally
        {
            ActivityOptionsRegistry.Replace(null, new Dictionary<string, ActivityOptions>(), null, new Dictionary<string, LocalActivityOptions>());
        }
    }

    [Fact]
    public void ResolveLocal_ReturnsDefaultOrNamed_AndThrowsWhenAbsent()
    {
        try
        {
            var localDefault = new LocalActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(1) };
            var localNamed = new LocalActivityOptions { ScheduleToCloseTimeout = TimeSpan.FromSeconds(5) };
            ActivityOptionsRegistry.Replace(
                null, new Dictionary<string, ActivityOptions>(),
                localDefault, new Dictionary<string, LocalActivityOptions> { ["long"] = localNamed });

            Assert.Equal(TimeSpan.FromSeconds(1), ActivityOptionsRegistry.ResolveLocal(null).StartToCloseTimeout);
            Assert.Equal(TimeSpan.FromSeconds(5), ActivityOptionsRegistry.ResolveLocal("long").ScheduleToCloseTimeout);
            Assert.Throws<KeyNotFoundException>(() => ActivityOptionsRegistry.ResolveLocal("missing"));
        }
        finally
        {
            ActivityOptionsRegistry.Replace(null, new Dictionary<string, ActivityOptions>(), null, new Dictionary<string, LocalActivityOptions>());
        }
    }

    [Fact]
    public void GetLocalDefault_FallsBackToBuiltIn_WhenNothingConfigured()
    {
        try
        {
            ActivityOptionsRegistry.Replace(null, new Dictionary<string, ActivityOptions>(), null, new Dictionary<string, LocalActivityOptions>());

            var options = ActivityOptionsRegistry.GetLocalDefault();

            Assert.Equal(TimeSpan.FromSeconds(10), options.ScheduleToCloseTimeout);
        }
        finally
        {
            ActivityOptionsRegistry.Replace(null, new Dictionary<string, ActivityOptions>(), null, new Dictionary<string, LocalActivityOptions>());
        }
    }
}

[Collection("ActivityOptionsRegistry")]
public class ActivityOptionsLocalConfigTests
{
    [Fact]
    public void AddTemporal_Configuration_SeedsBothRegistries()
    {
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Temporal:TargetHost"] = "host:7233",
                    ["Temporal:ActivityOptions:Default:ScheduleToCloseTimeout"] = "00:05:00",
                    ["Temporal:ActivityOptions:LocalDefault:StartToCloseTimeout"] = "00:00:10",
                    ["Temporal:ActivityOptions:Presets:long:ScheduleToCloseTimeout"] = "00:30:00",
                })
                .Build();

            var services = new ServiceCollection();
            services.AddTemporal(configuration);

            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<TemporalOptions>>().Value;

            Assert.NotNull(options.ActivityOptions);
            Assert.NotNull(options.ActivityOptions!.Default);
            Assert.NotNull(options.ActivityOptions.LocalDefault);
            Assert.Equal(TimeSpan.FromMinutes(5), options.ActivityOptions.Default!.ScheduleToCloseTimeout);
            Assert.Equal(TimeSpan.FromSeconds(10), options.ActivityOptions.LocalDefault!.StartToCloseTimeout);

            Assert.Equal(TimeSpan.FromMinutes(5), ActivityOptionsRegistry.GetDefault().ScheduleToCloseTimeout);
            Assert.Equal(TimeSpan.FromSeconds(10), ActivityOptionsRegistry.GetLocalDefault().StartToCloseTimeout);
            Assert.Equal(TimeSpan.FromMinutes(30), ActivityOptionsRegistry.Get("long").ScheduleToCloseTimeout);
            Assert.Equal(TimeSpan.FromMinutes(30), ActivityOptionsRegistry.GetLocal("long").ScheduleToCloseTimeout);
            Assert.Contains("long", ActivityOptionsRegistry.Names);
        }
        finally
        {
            ActivityOptionsRegistry.Replace(null, new Dictionary<string, ActivityOptions>(), null, new Dictionary<string, LocalActivityOptions>());
        }
    }

    [Fact]
    public void AddTemporal_Configuration_ActivityOptionsWithoutTimeout_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Temporal:TargetHost"] = "host:7233",
                ["Temporal:ActivityOptions:LocalDefault:LocalRetryThreshold"] = "00:00:15",
            })
            .Build();

        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddTemporal(configuration));
    }
}
