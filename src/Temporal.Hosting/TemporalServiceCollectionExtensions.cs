using System.Diagnostics.Metrics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Common;
using Temporalio.Converters;
using Temporalio.Extensions.Hosting;
using Temporalio.Runtime;
using Temporalio.Worker;
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
    /// the <c>Temporal</c> configuration section. The options are registered
    /// through the options infrastructure so <see cref="IOptionsMonitor{TemporalOptions}"/>
    /// reflects configuration reloads.
    /// </summary>
    public static TemporalBuilder AddTemporal(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(TemporalOptions.SectionName);
        services.Configure<TemporalOptions>(section);

        var options = new TemporalOptions();
        section.Bind(options);
        Validate(options);
        return RegisterCore(services, options);
    }

    /// <summary>
    /// Registers a Temporal client and starter services using an options
    /// configuration delegate.
    /// </summary>
    public static TemporalBuilder AddTemporal(this IServiceCollection services, Action<TemporalOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);

        var options = new TemporalOptions();
        configure(options);
        Validate(options);
        return RegisterCore(services, options);
    }

    /// <summary>
    /// Registers a Temporal client and starter services using the given options.
    /// </summary>
    public static TemporalBuilder AddTemporal(this IServiceCollection services, TemporalOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        Validate(options);

        services.Configure<TemporalOptions>(configured => CopyTo(options, configured));

        return RegisterCore(services, options);
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
        return builder.Services.AddTemporalWorker(taskQueue, assembly, configure);
    }

    /// <summary>
    /// Registers a hosted Temporal worker that opts into worker versioning via
    /// <see cref="WorkerDeploymentOptions"/> (public preview), applying
    /// convention-based auto-discovery.
    /// </summary>
    public static ITemporalWorkerServiceOptionsBuilder AddTemporalWorker(
        this TemporalBuilder builder,
        string taskQueue,
        WorkerDeploymentOptions deploymentOptions,
        Assembly? assembly = null,
        Action<TemporalWorkerServiceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Services.AddTemporalWorker(taskQueue, deploymentOptions, assembly, configure);
    }

    /// <summary>
    /// Registers a hosted Temporal worker that auto-discovers types from the
    /// assemblies of the given marker types. Use this instead of the assembly
    /// overload when the entry assembly is not the worker assembly (e.g. under
    /// <c>dotnet test</c>).
    /// </summary>
    public static ITemporalWorkerServiceOptionsBuilder AddTemporalWorker(
        this TemporalBuilder builder,
        string taskQueue,
        Type markerType,
        params Type[] markerTypes)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Services.AddTemporalWorker(taskQueue, markerType, markerTypes);
    }

    /// <summary>
    /// Registers a hosted Temporal worker that auto-discovers types from the
    /// assemblies of the given marker types and applies a worker options
    /// configuration delegate.
    /// </summary>
    public static ITemporalWorkerServiceOptionsBuilder AddTemporalWorker(
        this TemporalBuilder builder,
        string taskQueue,
        Type markerType,
        Action<TemporalWorkerServiceOptions> configure,
        params Type[] markerTypes)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Services.AddTemporalWorker(taskQueue, markerType, configure, markerTypes);
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
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ITemporalWorkerServiceOptionsBuilder AddTemporalWorker(
        this IServiceCollection services,
        string taskQueue,
        Assembly? assembly = null,
        Action<TemporalWorkerServiceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(taskQueue);

        return AddTemporalWorkerCore(
            services,
            taskQueue,
            deploymentOptions: null,
            ResolveAssemblies(assembly, Array.Empty<Type>(), Assembly.GetCallingAssembly()),
            configure);
    }

    /// <summary>
    /// Registers a hosted Temporal worker that opts into worker versioning via
    /// <see cref="WorkerDeploymentOptions"/> (public preview), applying
    /// convention-based auto-discovery.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ITemporalWorkerServiceOptionsBuilder AddTemporalWorker(
        this IServiceCollection services,
        string taskQueue,
        WorkerDeploymentOptions deploymentOptions,
        Assembly? assembly = null,
        Action<TemporalWorkerServiceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(taskQueue);
        ArgumentNullException.ThrowIfNull(deploymentOptions);

        if (deploymentOptions.Version is null)
        {
            throw new ArgumentException("Deployment version must be set when using worker versioning.", nameof(deploymentOptions));
        }

        return AddTemporalWorkerCore(
            services,
            taskQueue,
            deploymentOptions,
            ResolveAssemblies(assembly, Array.Empty<Type>(), Assembly.GetCallingAssembly()),
            configure);
    }

    /// <summary>
    /// Registers a hosted Temporal worker that auto-discovers types from the
    /// assemblies of the given marker types.
    /// </summary>
    public static ITemporalWorkerServiceOptionsBuilder AddTemporalWorker(
        this IServiceCollection services,
        string taskQueue,
        Type markerType,
        params Type[] markerTypes)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(taskQueue);
        ArgumentNullException.ThrowIfNull(markerType);

        return AddTemporalWorkerCore(
            services,
            taskQueue,
            deploymentOptions: null,
            ResolveAssemblies(null, Prepend(markerType, markerTypes), Assembly.GetCallingAssembly()),
            configure: null);
    }

    /// <summary>
    /// Registers a hosted Temporal worker that auto-discovers types from the
    /// assemblies of the given marker types and applies a worker options
    /// configuration delegate.
    /// </summary>
    public static ITemporalWorkerServiceOptionsBuilder AddTemporalWorker(
        this IServiceCollection services,
        string taskQueue,
        Type markerType,
        Action<TemporalWorkerServiceOptions> configure,
        params Type[] markerTypes)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(taskQueue);
        ArgumentNullException.ThrowIfNull(markerType);
        ArgumentNullException.ThrowIfNull(configure);

        return AddTemporalWorkerCore(
            services,
            taskQueue,
            deploymentOptions: null,
            ResolveAssemblies(null, Prepend(markerType, markerTypes), Assembly.GetCallingAssembly()),
            configure);
    }

    private static TemporalBuilder RegisterCore(IServiceCollection services, TemporalOptions options)
    {
        services.AddSingleton<IValidateOptions<TemporalOptions>, TemporalOptionsValidator>();

        var exportMetrics = !string.IsNullOrWhiteSpace(options.Metrics.PrometheusBindAddress)
            || !string.IsNullOrWhiteSpace(options.Metrics.OpenTelemetryUrl);
        var forwardLogs = options.Logging.Enabled;
        var needsRuntime = exportMetrics || forwardLogs;

        var payloadCodec = TemporalDataConverterFactory.BuildCodec(options.DataConverter);
        var dataConverter = payloadCodec is null
            ? DataConverter.Default
            : DataConverter.Default with { PayloadCodec = payloadCodec };

        if (options.Metrics.Enabled)
        {
            services.AddSingleton(sp => new Meter(sp.GetRequiredService<IOptions<TemporalOptions>>().Value.Metrics.MeterName));
            services.AddSingleton<TemporalMetricsInterceptor>();
        }

        if (needsRuntime)
        {
            services.AddSingleton(sp => CreateRuntime(options.Metrics, options.Logging, sp.GetService<ILoggerFactory>()));
        }

        // The payload codec is registered as a singleton so a codec server hosted
        // in the same app (see Kogoshvili.Temporal.CodecServer) can resolve the
        // exact same instance the client and workers encode/decode with.
        if (payloadCodec is not null)
        {
            services.AddSingleton(payloadCodec);
        }

        if (options.TestServer.Enabled)
        {
            // A single connect-options instance is shared between the lazy client
            // and the test-server service. The lazy connection reads TargetHost on
            // first connect, so the service can fill it in once the dev server has
            // bound an (ephemeral) port.
            services.AddSingleton(sp =>
            {
                var connect = new TemporalClientConnectOptions
                {
                    Namespace = options.Namespace,
                    DataConverter = dataConverter,
                };
                if (needsRuntime)
                {
                    connect.Runtime = sp.GetRequiredService<TemporalRuntime>();
                }

                connect.LoggerFactory = sp.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
                return connect;
            });
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
            if (needsRuntime)
            {
                client.Configure<TemporalRuntime>((connect, runtime) => connect.Runtime = runtime);
            }

            client.Configure(connect => connect.DataConverter = dataConverter);

            if (options.Metrics.Enabled)
            {
                client.Configure<TemporalMetricsInterceptor>((connect, interceptor) =>
                    connect.Interceptors = (connect.Interceptors ?? Array.Empty<IClientInterceptor>())
                        .Concat(new IClientInterceptor[] { interceptor })
                        .ToArray());
            }

            // Resolve cloud TLS certificate material before the connection waiter
            // connects (and therefore before any worker polls). Registered before
            // the waiter so hosted services start in the right order.
            services.AddSingleton<IHostedService, TemporalCertificateLoader>();

            // Wait for the server to be reachable before workers poll. Registered
            // here (during AddTemporal) so it starts before any worker service.
            services.AddSingleton<IHostedService, TemporalConnectionWaiter>();
        }

        return new TemporalBuilder(services);
    }

    private static ITemporalWorkerServiceOptionsBuilder AddTemporalWorkerCore(
        IServiceCollection services,
        string taskQueue,
        WorkerDeploymentOptions? deploymentOptions,
        IReadOnlyCollection<Assembly> assemblies,
        Action<TemporalWorkerServiceOptions>? configure)
    {
        var worker = services.AddHostedTemporalWorker(taskQueue, deploymentOptions);

        foreach (var workflowType in assemblies.SelectMany(WorkerDiscovery.FindWorkflowTypes))
        {
            worker.AddWorkflow(workflowType);
        }

        foreach (var activityType in assemblies.SelectMany(WorkerDiscovery.FindActivityTypes))
        {
            switch (WorkerDiscovery.GetActivityLifetime(activityType))
            {
                case ActivityLifetime.Singleton:
                    worker.AddSingletonActivities(activityType);
                    break;
                case ActivityLifetime.Transient:
                    worker.AddTransientActivities(activityType);
                    break;
                case ActivityLifetime.Static:
                    worker.AddStaticActivities(activityType);
                    break;
                default:
                    worker.AddScopedActivities(activityType);
                    break;
            }
        }

        if (configure is not null)
        {
            worker.ConfigureOptions(configure);
        }

        return worker;
    }

    private static IReadOnlyCollection<Assembly> ResolveAssemblies(
        Assembly? assembly,
        Type[] markerTypes,
        Assembly callingAssembly)
    {
        if (assembly is not null)
        {
            return new[] { assembly };
        }

        if (markerTypes.Length > 0)
        {
            return markerTypes.Select(type => type.Assembly).Distinct().ToArray();
        }

        return new[] { Assembly.GetEntryAssembly() ?? callingAssembly };
    }

    private static Type[] Prepend(Type first, Type[] rest)
    {
        var types = new Type[rest.Length + 1];
        types[0] = first;
        Array.Copy(rest, 0, types, 1, rest.Length);
        return types;
    }

    private static TemporalRuntime CreateRuntime(
        TemporalMetricsOptions metrics,
        TemporalLoggingOptions logging,
        ILoggerFactory? loggerFactory)
    {
        MetricsOptions? metricsOptions = null;
        if (!string.IsNullOrWhiteSpace(metrics.PrometheusBindAddress))
        {
            metricsOptions = new MetricsOptions(new PrometheusOptions(metrics.PrometheusBindAddress));
        }
        else if (!string.IsNullOrWhiteSpace(metrics.OpenTelemetryUrl))
        {
            metricsOptions = new MetricsOptions(new OpenTelemetryOptions(metrics.OpenTelemetryUrl));
        }

        LoggingOptions? loggingOptions = null;
        if (logging.Enabled)
        {
            if (loggerFactory is null)
            {
                throw new InvalidOperationException(
                    "Temporal:Logging is enabled but no ILoggerFactory is registered in the service container.");
            }

            loggingOptions = new LoggingOptions
            {
                Forwarding = new LogForwardingOptions
                {
                    Logger = loggerFactory.CreateLogger(logging.Category),
                },
            };
        }

        return new TemporalRuntime(new TemporalRuntimeOptions(new TelemetryOptions
        {
            Metrics = metricsOptions,
            Logging = loggingOptions,
        }));
    }

    private static void Validate(TemporalOptions options)
    {
        TemporalOptionsValidation.Validate(options);
    }

    private static void CopyTo(TemporalOptions source, TemporalOptions target)
    {
        target.TargetHost = source.TargetHost;
        target.Namespace = source.Namespace;
        target.ApiKey = source.ApiKey;
        target.Tls = source.Tls;
        target.RpcRetry = source.RpcRetry;
        target.Metrics = source.Metrics;
        target.Logging = source.Logging;
        target.TestServer = source.TestServer;
        target.ConnectionWait = source.ConnectionWait;
        target.DataConverter = source.DataConverter;
    }
}
