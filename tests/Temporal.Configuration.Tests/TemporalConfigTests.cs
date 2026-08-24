using Kogoshvili.Temporal.Configuration;
using Microsoft.Extensions.Configuration;

namespace Kogoshvili.Temporal.Configuration.Tests;

public class TemporalConfigTests
{
    [Fact]
    public void Load_BindsConnectionOptions_FromConfigurationSection()
    {
        var config = BuildConfig("""
            {
              "Temporal": {
                "TargetHost": "my-host:7233",
                "Namespace": "my-ns",
                "ApiKey": "secret",
                "Tls": { "Domain": "example.com" }
              }
            }
            """);

        var options = TemporalConfig.Load(config);

        Assert.Equal("my-host:7233", options.TargetHost);
        Assert.Equal("my-ns", options.Namespace);
        Assert.Equal("secret", options.ApiKey);
        Assert.NotNull(options.Tls);
        Assert.Equal("example.com", options.Tls!.Domain);
    }

    [Fact]
    public void Load_AppliesDefaults_WhenSectionMissing()
    {
        var config = new ConfigurationBuilder().Build();

        var options = TemporalConfig.Load(config);

        Assert.Equal("localhost:7233", options.TargetHost);
        Assert.Equal("default", options.Namespace);
        Assert.Null(options.ApiKey);
        Assert.Null(options.Tls);
    }

    [Fact]
    public void Load_LaterConfigurationSource_Wins()
    {
        // Mirrors BuildConfiguration's ordering: appsettings.json first, then
        // environment variables (Temporal__*). The later source overrides.
        var baseFile = WriteTempJson("""{ "Temporal": { "TargetHost": "json-host:7233", "Namespace": "json-ns" } }""");
        var overrideFile = WriteTempJson("""{ "Temporal": { "TargetHost": "env-host:7233" } }""");
        try
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile(baseFile)
                .AddJsonFile(overrideFile)
                .Build();

            var options = TemporalConfig.Load(config);

            Assert.Equal("env-host:7233", options.TargetHost);
            Assert.Equal("json-ns", options.Namespace);
        }
        finally
        {
            File.Delete(baseFile);
            File.Delete(overrideFile);
        }
    }

    private static IConfiguration BuildConfig(string json)
    {
        var path = WriteTempJson(json);
        try
        {
            return new ConfigurationBuilder().AddJsonFile(path).Build();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTempJson(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"temporal-config-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
