using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Common;
using Temporalio.Worker;

namespace Kogoshvili.Temporal.Hosting.Tests;

public class WorkerDeploymentOptionsTests
{
    private static IConfiguration BuildConfiguration(params (string Key, string? Value)[] values)
    {
        var data = new Dictionary<string, string?>
        {
            ["Temporal:TargetHost"] = "host:7233",
        };

        foreach (var (key, value) in values)
        {
            data[key] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    [Fact]
    public void AddTemporalWorker_DeploymentConfig_ResolvesVersioning()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal(BuildConfiguration(
            ("Temporal:Workers:q:Deployment:DeploymentName", "hosted-app"),
            ("Temporal:Workers:q:Deployment:BuildId", "1.0"),
            ("Temporal:Workers:q:Deployment:UseWorkerVersioning", "true"),
            ("Temporal:Workers:q:Deployment:DefaultVersioningBehavior", "Pinned")));

        var builder = services.AddTemporalWorker("q");

        Assert.NotNull(builder.DeploymentOptions);
        Assert.NotNull(builder.DeploymentOptions!.Version);
        Assert.Equal("hosted-app", builder.DeploymentOptions.Version.DeploymentName);
        Assert.Equal("1.0", builder.DeploymentOptions.Version.BuildId);
        Assert.True(builder.DeploymentOptions.UseWorkerVersioning);
        Assert.Equal(VersioningBehavior.Pinned, builder.DeploymentOptions.DefaultVersioningBehavior);
    }

    [Fact]
    public void AddTemporalWorker_VersionAlias_ResolvesLikeBuildId()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal(BuildConfiguration(
            ("Temporal:Workers:q:Deployment:DeploymentName", "hosted-app"),
            ("Temporal:Workers:q:Deployment:Version", "2.0"),
            ("Temporal:Workers:q:Deployment:UseWorkerVersioning", "true")));

        var builder = services.AddTemporalWorker("q");

        Assert.NotNull(builder.DeploymentOptions);
        Assert.Equal("2.0", builder.DeploymentOptions!.Version!.BuildId);
    }

    [Fact]
    public void AddTemporalWorker_BuildIdWinsOverVersion()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal(BuildConfiguration(
            ("Temporal:Workers:q:Deployment:DeploymentName", "hosted-app"),
            ("Temporal:Workers:q:Deployment:BuildId", "1.0"),
            ("Temporal:Workers:q:Deployment:Version", "2.0"),
            ("Temporal:Workers:q:Deployment:UseWorkerVersioning", "true")));

        var builder = services.AddTemporalWorker("q");

        Assert.Equal("1.0", builder.DeploymentOptions!.Version!.BuildId);
    }

    [Fact]
    public void AddTemporalWorker_NoDeployment_NullDeploymentOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal(BuildConfiguration());

        var builder = services.AddTemporalWorker("q");

        Assert.Null(builder.DeploymentOptions);
    }

    [Fact]
    public void AddTemporalWorker_UseWorkerVersioningOmitted_Unversioned()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal(BuildConfiguration(
            ("Temporal:Workers:q:Deployment:DeploymentName", "hosted-app"),
            ("Temporal:Workers:q:Deployment:BuildId", "1.0")));

        var builder = services.AddTemporalWorker("q");

        Assert.Null(builder.DeploymentOptions);
    }

    [Fact]
    public void AddTemporalWorker_UseWorkerVersioningFalse_Unversioned()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal(BuildConfiguration(
            ("Temporal:Workers:q:Deployment:DeploymentName", "hosted-app"),
            ("Temporal:Workers:q:Deployment:BuildId", "1.0"),
            ("Temporal:Workers:q:Deployment:UseWorkerVersioning", "false")));

        var builder = services.AddTemporalWorker("q");

        Assert.Null(builder.DeploymentOptions);
    }

    [Fact]
    public void AddTemporalWorker_DefaultVersioningBehaviorOmitted_Unspecified()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal(BuildConfiguration(
            ("Temporal:Workers:q:Deployment:DeploymentName", "hosted-app"),
            ("Temporal:Workers:q:Deployment:BuildId", "1.0"),
            ("Temporal:Workers:q:Deployment:UseWorkerVersioning", "true")));

        var builder = services.AddTemporalWorker("q");

        Assert.Equal(VersioningBehavior.Unspecified, builder.DeploymentOptions!.DefaultVersioningBehavior);
    }

    [Fact]
    public void AddTemporalWorker_ExplicitArgumentWinsOverConfig()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal(BuildConfiguration(
            ("Temporal:Workers:q:Deployment:DeploymentName", "hosted-app"),
            ("Temporal:Workers:q:Deployment:BuildId", "1.0"),
            ("Temporal:Workers:q:Deployment:UseWorkerVersioning", "true")));

        var builder = services.AddTemporalWorker(
            "q",
            new WorkerDeploymentOptions(new WorkerDeploymentVersion("explicit-app", "9.9"), useWorkerVersioning: true));

        Assert.Equal("explicit-app", builder.DeploymentOptions!.Version!.DeploymentName);
        Assert.Equal("9.9", builder.DeploymentOptions.Version.BuildId);
    }

    [Fact]
    public void AddTemporalWorker_VersioningMissingDeploymentName_Throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal(BuildConfiguration(
            ("Temporal:Workers:q:Deployment:BuildId", "1.0"),
            ("Temporal:Workers:q:Deployment:UseWorkerVersioning", "true")));

        Assert.Throws<ArgumentException>(() => services.AddTemporalWorker("q"));
    }

    [Fact]
    public void AddTemporalWorker_VersioningMissingBuildId_Throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal(BuildConfiguration(
            ("Temporal:Workers:q:Deployment:DeploymentName", "hosted-app"),
            ("Temporal:Workers:q:Deployment:UseWorkerVersioning", "true")));

        Assert.Throws<ArgumentException>(() => services.AddTemporalWorker("q"));
    }
}
