using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;

namespace Kogoshvili.Temporal.Hosting.Tests;

public class WorkflowOptionsFactoryTests
{
    [Fact]
    public void Apply_MapsConfiguredValues()
    {
        var preset = new WorkflowOptionsPreset
        {
            RunTimeout = TimeSpan.FromMinutes(5),
            TaskTimeout = TimeSpan.FromSeconds(10),
            IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting,
            Retry = new RetryPolicyOptions { InitialInterval = TimeSpan.FromSeconds(2), MaximumAttempts = 3 },
        };

        var options = new WorkflowOptions { Id = "id", TaskQueue = "queue" };
        WorkflowOptionsFactory.Apply(preset, options);

        Assert.Equal(TimeSpan.FromMinutes(5), options.RunTimeout);
        Assert.Equal(TimeSpan.FromSeconds(10), options.TaskTimeout);
        Assert.Equal(WorkflowIdConflictPolicy.UseExisting, options.IdConflictPolicy);
        Assert.NotNull(options.RetryPolicy);
        Assert.Equal(TimeSpan.FromSeconds(2), options.RetryPolicy!.InitialInterval);
        Assert.Equal(3, options.RetryPolicy.MaximumAttempts);
        Assert.Equal("id", options.Id);
        Assert.Equal("queue", options.TaskQueue);
    }

    [Fact]
    public void Apply_NullPreset_LeavesOptionsUntouched()
    {
        var options = new WorkflowOptions { Id = "id", TaskQueue = "queue" };

        WorkflowOptionsFactory.Apply(null, options);

        Assert.Null(options.RunTimeout);
        Assert.Null(options.RetryPolicy);
    }

    [Fact]
    public void Apply_WithoutRetry_LeavesRetryPolicyNull()
    {
        var preset = new WorkflowOptionsPreset { RunTimeout = TimeSpan.FromSeconds(1) };

        var options = new WorkflowOptions { Id = "id", TaskQueue = "queue" };
        WorkflowOptionsFactory.Apply(preset, options);

        Assert.Equal(TimeSpan.FromSeconds(1), options.RunTimeout);
        Assert.Null(options.RetryPolicy);
    }
}

public class WorkflowOptionsRegistryTests
{
    private static WorkflowOptionsRegistry CreateRegistry(TemporalWorkflowOptions workflows) =>
        new(Options.Create(new TemporalOptions { Workflows = workflows }));

    [Fact]
    public void Build_AppliesDefaultPreset()
    {
        var registry = CreateRegistry(new TemporalWorkflowOptions
        {
            Default = new WorkflowOptionsPreset { RunTimeout = TimeSpan.FromMinutes(5) },
        });

        var options = registry.Build("MyWorkflow", "queue");

        Assert.Equal("queue", options.TaskQueue);
        Assert.Equal(TimeSpan.FromMinutes(5), options.RunTimeout);
        Assert.Null(options.Id);
    }

    [Fact]
    public void Build_PerTypeOverridesDefault()
    {
        var registry = CreateRegistry(new TemporalWorkflowOptions
        {
            Default = new WorkflowOptionsPreset { RunTimeout = TimeSpan.FromMinutes(5) },
            ByType = new Dictionary<string, WorkflowOptionsPreset>
            {
                ["MyWorkflow"] = new WorkflowOptionsPreset { RunTimeout = TimeSpan.FromMinutes(30) },
            },
        });

        var options = registry.Build("MyWorkflow", "queue");

        Assert.Equal(TimeSpan.FromMinutes(30), options.RunTimeout);
    }

    [Fact]
    public void Build_UnknownType_LeavesDefault()
    {
        var registry = CreateRegistry(new TemporalWorkflowOptions
        {
            Default = new WorkflowOptionsPreset { RunTimeout = TimeSpan.FromMinutes(5) },
        });

        var options = registry.Build("OtherWorkflow", "queue");

        Assert.Equal(TimeSpan.FromMinutes(5), options.RunTimeout);
    }

    [Fact]
    public void Build_CallerOverrideWins()
    {
        var registry = CreateRegistry(new TemporalWorkflowOptions
        {
            Default = new WorkflowOptionsPreset { RunTimeout = TimeSpan.FromMinutes(5) },
        });

        var options = registry.Build("MyWorkflow", "queue", configure: o => o.RunTimeout = TimeSpan.FromSeconds(1));

        Assert.Equal(TimeSpan.FromSeconds(1), options.RunTimeout);
    }

    [Fact]
    public void Build_ExplicitIdWinsOverConvention()
    {
        var registry = CreateRegistry(new TemporalWorkflowOptions
        {
            Id = new WorkflowIdOptions { Format = "{Type}-{Guid:N}" },
        });

        var options = registry.Build("MyWorkflow", "queue", workflowId: "explicit-id");

        Assert.Equal("explicit-id", options.Id);
    }

    [Fact]
    public void Build_IdConvention_SubstitutesTypeQueueAndGuid()
    {
        var registry = CreateRegistry(new TemporalWorkflowOptions
        {
            Id = new WorkflowIdOptions { Format = "{Type}-{Queue}-{Guid:N}" },
        });

        var options = registry.Build("MyWorkflow", "queue-a");

        Assert.Matches(@"^MyWorkflow-queue-a-[0-9a-f]{32}$", options.Id!);
    }

    [Fact]
    public void Build_NoIdAndNoFormat_LeavesIdNull()
    {
        var registry = CreateRegistry(new TemporalWorkflowOptions());

        var options = registry.Build("MyWorkflow", "queue");

        Assert.Null(options.Id);
    }

    [Fact]
    public void AddTemporal_Configuration_BindsWorkflowsAndRegistersRegistry()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Temporal:TargetHost"] = "host:7233",
                ["Temporal:Workflows:Id:Format"] = "{Type}-{Guid:N}",
                ["Temporal:Workflows:Default:RunTimeout"] = "00:05:00",
                ["Temporal:Workflows:ByType:MyWorkflow:RunTimeout"] = "00:30:00",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTemporal(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<TemporalOptions>>().Value;

        Assert.NotNull(options.Workflows);
        Assert.Equal("{Type}-{Guid:N}", options.Workflows!.Id!.Format);
        Assert.Equal(TimeSpan.FromMinutes(5), options.Workflows.Default!.RunTimeout);
        Assert.Equal(TimeSpan.FromMinutes(30), options.Workflows.ByType!["MyWorkflow"].RunTimeout);

        var registry = provider.GetRequiredService<WorkflowOptionsRegistry>();
        var built = registry.Build("MyWorkflow", "queue");
        Assert.Equal(TimeSpan.FromMinutes(30), built.RunTimeout);
        Assert.Matches(@"^MyWorkflow-[0-9a-f]{32}$", built.Id!);
    }
}
