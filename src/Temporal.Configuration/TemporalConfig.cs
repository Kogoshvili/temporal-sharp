using Microsoft.Extensions.Configuration;
using Temporalio.Client;

namespace Kogoshvili.Temporal.Configuration;

/// <summary>
/// Loads a Temporal connection from configuration and creates an authenticated
/// <see cref="ITemporalClient"/>. The connection binds from the <c>Temporal</c>
/// section of <c>appsettings.json</c>, overridable by <c>Temporal__*</c>
/// environment variables (e.g. <c>Temporal__TargetHost</c>,
/// <c>Temporal__ApiKey</c>, <c>Temporal__Tls__ClientCertPath</c>).
/// </summary>
public static class TemporalConfig
{
    /// <summary>
    /// Builds configuration from an optional JSON file plus environment
    /// variables. When <paramref name="appSettingsPath"/> is provided it is
    /// required; otherwise an <c>appsettings.json</c> in the current directory
    /// is loaded if present.
    /// </summary>
    public static IConfigurationRoot BuildConfiguration(string? appSettingsPath = null)
    {
        var builder = new ConfigurationBuilder();
        if (appSettingsPath is not null)
        {
            builder.AddJsonFile(appSettingsPath, optional: false);
        }
        else
        {
            builder.AddJsonFile("appsettings.json", optional: true);
        }

        return builder.AddEnvironmentVariables().Build();
    }

    /// <summary>Binds connection options from an existing configuration root.</summary>
    public static TemporalConnectionOptions Load(IConfiguration configuration)
    {
        var options = new TemporalConnectionOptions();
        configuration.GetSection(TemporalConnectionOptions.SectionName).Bind(options);
        return options;
    }

    /// <summary>Loads connection options from <c>appsettings.json</c> + environment variables.</summary>
    public static TemporalConnectionOptions Load() => Load(BuildConfiguration());

    /// <summary>Converts connection options to connect options.</summary>
    public static TemporalClientConnectOptions ToConnectOptions(TemporalConnectionOptions options)
    {
        var connect = new TemporalClientConnectOptions();
        ClientOptionsFactory.Apply(connect, options);
        return connect;
    }

    /// <summary>Connects using the given connection options.</summary>
    public static async Task<ITemporalClient> ConnectAsync(TemporalConnectionOptions options) =>
        await TemporalClient.ConnectAsync(ToConnectOptions(options));

    /// <summary>Connects using options bound from an existing configuration root.</summary>
    public static Task<ITemporalClient> ConnectAsync(IConfiguration configuration) =>
        ConnectAsync(Load(configuration));

    /// <summary>Connects using options loaded from <c>appsettings.json</c> + environment variables.</summary>
    public static Task<ITemporalClient> ConnectAsync() => ConnectAsync(Load());
}
