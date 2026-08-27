using System.Text.Json;
using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kogoshvili.Temporal.Hosting.Tests;

public class WorkflowSettingsActivityTests
{
    private static WorkflowSettingsActivity CreateActivity(TemporalWorkflowSettings settings)
    {
        var services = new ServiceCollection();
        services.Configure<TemporalOptions>(o => o.WorkflowSettings = settings);
        using var provider = services.BuildServiceProvider();
        return new WorkflowSettingsActivity(provider.GetRequiredService<IOptionsMonitor<TemporalOptions>>());
    }

    private static Dictionary<string, JsonElement> Read(WorkflowSettingsActivity activity, string workflowType)
    {
        var json = activity.Read(workflowType);
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
    }

    [Fact]
    public void Read_MergesByTypeOverDefault()
    {
        var activity = CreateActivity(new TemporalWorkflowSettings
        {
            Default = new Dictionary<string, object?> { ["batchSize"] = "10", ["shared"] = "yes" },
            ByType = new Dictionary<string, Dictionary<string, object?>>
            {
                ["MyWorkflow"] = new() { ["batchSize"] = "100" },
            },
        });

        var settings = Read(activity, "MyWorkflow");

        Assert.Equal(100, settings["batchSize"].GetInt32());
        Assert.Equal("yes", settings["shared"].GetString());
    }

    [Fact]
    public void Read_NoByType_ReturnsDefaultOnly()
    {
        var activity = CreateActivity(new TemporalWorkflowSettings
        {
            Default = new Dictionary<string, object?> { ["batchSize"] = "10" },
        });

        var settings = Read(activity, "OtherWorkflow");

        Assert.Equal(10, settings["batchSize"].GetInt32());
    }

    [Fact]
    public void Read_NoSettings_ReturnsEmptyObject()
    {
        var activity = CreateActivity(new TemporalWorkflowSettings());

        var settings = Read(activity, "MyWorkflow");

        Assert.Empty(settings);
    }

    [Fact]
    public void Read_ConvertsConfigStringsToTypedValues()
    {
        var activity = CreateActivity(new TemporalWorkflowSettings
        {
            Default = new Dictionary<string, object?>
            {
                ["batchSize"] = "10",
                ["enabled"] = "true",
                ["ratio"] = "1.5",
                ["endpoint"] = "https://api",
            },
        });

        var settings = Read(activity, "MyWorkflow");

        Assert.Equal(JsonValueKind.Number, settings["batchSize"].ValueKind);
        Assert.Equal(10, settings["batchSize"].GetInt32());
        Assert.Equal(JsonValueKind.True, settings["enabled"].ValueKind);
        Assert.Equal(1.5, settings["ratio"].GetDouble());
        Assert.Equal("https://api", settings["endpoint"].GetString());
    }

    [Fact]
    public void Read_DeserializesIntoTypedSettings()
    {
        var activity = CreateActivity(new TemporalWorkflowSettings
        {
            Default = new Dictionary<string, object?>
            {
                ["batchSize"] = "10",
                ["enabled"] = "true",
                ["endpoint"] = "https://api",
            },
        });

        var settings = JsonSerializer.Deserialize<MyWorkflowSettings>(
            activity.Read("MyWorkflow"),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Equal(10, settings.BatchSize);
        Assert.True(settings.Enabled);
        Assert.Equal("https://api", settings.Endpoint);
    }

    private sealed class MyWorkflowSettings
    {
        public int BatchSize { get; set; }

        public bool Enabled { get; set; }

        public string? Endpoint { get; set; }
    }
}

public class WorkflowSettingsRegistrationTests
{
    [Fact]
    public void AddTemporal_Configuration_BindsWorkflowSettings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Temporal:TargetHost"] = "host:7233",
                ["Temporal:WorkflowSettings:Default:batchSize"] = "10",
                ["Temporal:WorkflowSettings:ByType:MyWorkflow:batchSize"] = "100",
                ["Temporal:WorkflowSettings:ByType:MyWorkflow:endpoint"] = "https://api",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTemporal(configuration);

        using var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IOptions<TemporalOptions>>().Value.WorkflowSettings;

        Assert.NotNull(settings);
        Assert.Equal("10", settings!.Default!["batchSize"]);
        Assert.Equal("100", settings.ByType!["MyWorkflow"]["batchSize"]);
        Assert.Equal("https://api", settings.ByType["MyWorkflow"]["endpoint"]);
    }

    [Fact]
    public void AddTemporalWorker_RegistersWorkflowSettingsActivity()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal();
        services.AddTemporalWorker("queue");

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<WorkflowSettingsActivity>());
    }
}
