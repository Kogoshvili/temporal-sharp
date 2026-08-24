using System.Diagnostics.Metrics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Extensions.Hosting;
using Kogoshvili.Temporal.Configuration;
using Kogoshvili.Temporal.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Generic-host worker starter extensions that register a Temporal client and
/// hosted workers, mirroring the Java Spring Boot Temporal starter.
/// </summary>
public static class TemporalServiceCollectionExtensions
{
    /// <summary>
    /// Registers a Temporal client and starter services using default options.
    /// </summary>
    public static TemporalBuilder AddTemporal(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddTemporal(new TemporalOptions());
    }

    /// <summary>
    /// Registers a Temporal client and starter services, binding options from
    /// the <c>Temporal</c> configuration section.
    /// </summary>
    public static TemporalBuilder AddTemporal(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new TemporalOptions();
        configuration.GetSection(TemporalOptions.SectionName).Bind(options);
        return services.AddTemporal(options);
    }

    /// <summary>
    /// Registers a Temporal client and starter services using an options
    /// configuration delegate.
    /// </summary>
    public static TemporalBuilder AddTemporal(this IServiceCollection services, Action<TemporalOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new TemporalOptions();
        configure(options);
        return services.AddTemporal(options);
    }

    /// <summary>
    /// Registers a Temporal client and starter services using the given options.
    /// </summary>
    public static TemporalBuilder AddTemporal(this IServiceCollection services, TemporalOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.AddSingleton<IOptions<TemporalOptions>>(new OptionsWrapper<TemporalOptions>(options));

        if (options.Metrics.Enabled)
        {
            services.AddSingleton(sp => new Meter(sp.GetRequiredService<TemporalOptions>().Metrics.MeterName));
            services.AddSingleton<TemporalMetricsInterceptor>();
        }

        if (options.TestServer.Enabled)
        {
            // A single connect-options instance is shared between the lazy client
            // and the test-server service. The lazy connection reads TargetHost on
            // first connect, so the service can fill it in once the dev server has
            // bound an (ephemeral) port.
            var testConnectOptions = new TemporalClientConnectOptions { Namespace = options.Namespace };
            services.AddSingleton(testConnectOptions);
            services.AddSingleton<TemporalTestServerService>();
            services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<TemporalTestServerService>());
            services.AddSingleton<ITemporalClient>(sp =>
            {
                var connect = sp.GetRequiredService<TemporalClientConnectOptions>();
                if (sp.GetService<TemporalMetricsInterceptor>() is { } interceptor)
                {
                    connect.Interceptors = new IClientInterceptor[] { interceptor };
                }

                return TemporalClient.CreateLazy(connect);
            });
        }
        else
        {
            var client = services.AddTemporalClient();
            client.Configure(connect => ClientOptionsFactory.Apply(connect, options));
            if (options.Metrics.Enabled)
            {
                client.Configure<TemporalMetricsInterceptor>((connect, interceptor) =>
                    connect.Interceptors = (connect.Interceptors ?? Array.Empty<IClientInterceptor>())
                        .Concat(new IClientInterceptor[] { interceptor })
                        .ToArray());
            }
        }

        return new TemporalBuilder(services);
    }

    /// <summary>
    /// Registers a hosted Temporal worker for the given task queue, applying
    /// convention-based auto-discovery of <c>[Workflow]</c> and
    /// <c>[Activity]</c> types in the target assembly.
    /// </summary>
    /// <param name="builder">Builder returned by <c>AddTemporal</c>.</param>
    /// <param name="taskQueue">Task queue the worker polls.</param>
    /// <param name="assembly">Assembly to scan for workflow/activity types. Defaults to the entry assembly.</param>
    /// <param name="configure">Optional worker options configuration.</param>
    /// <returns>The underlying Temporal worker options builder for further configuration.</returns>
    public static ITemporalWorkerServiceOptionsBuilder AddTemporalWorker(
        this TemporalBuilder builder,
        string taskQueue,
        Assembly? assembly = null,
        Action<TemporalWorkerServiceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(taskQueue);

        return builder.Services.AddTemporalWorker(taskQueue, assembly, configure);
    }

    /// <summary>
    /// Registers a hosted Temporal worker for the given task queue, applying
    /// convention-based auto-discovery of <c>[Workflow]</c> and
    /// <c>[Activity]</c> types in the target assembly.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="taskQueue">Task queue the worker polls.</param>
    /// <param name="assembly">Assembly to scan for workflow/activity types. Defaults to the entry assembly.</param>
    /// <param name="configure">Optional worker options configuration.</param>
    /// <returns>The underlying Temporal worker options builder for further configuration.</returns>
    public static ITemporalWorkerServiceOptionsBuilder AddTemporalWorker(
        this IServiceCollection services,
        string taskQueue,
        Assembly? assembly = null,
        Action<TemporalWorkerServiceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(taskQueue);

        var targetAssembly = assembly ?? Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();
        var worker = services.AddHostedTemporalWorker(taskQueue);

        foreach (var workflowType in WorkerDiscovery.FindWorkflowTypes(targetAssembly))
        {
            worker.AddWorkflow(workflowType);
        }

        foreach (var activityType in WorkerDiscovery.FindActivityTypes(targetAssembly))
        {
            if (activityType.IsAbstract && activityType.IsSealed)
            {
                worker.AddStaticActivities(activityType);
            }
            else
            {
                worker.AddScopedActivities(activityType);
            }
        }

        if (configure is not null)
        {
            worker.ConfigureOptions(configure);
        }

        return worker;
    }
}
