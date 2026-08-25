using System.Diagnostics.Metrics;
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
    /// Registers a hosted Temporal worker for the given task queue. No workflow
    /// or activity types are registered automatically — register them explicitly
    /// on the returned builder (e.g. <c>AddWorkflow{T}()</c>,
    /// <c>AddSingletonActivities{T}()</c>), or opt into convention-based
    /// auto-discovery with <see cref="TemporalWorkerDiscoveryExtensions.AddDiscoveredTypes"/>.
    /// </summary>
    /// <param name="builder">Builder returned by <c>AddTemporal</c>.</param>
    /// <param name="taskQueue">Task queue the worker polls.</param>
    /// <param name="configure">Optional worker options configuration.</param>
    /// <returns>The underlying Temporal worker options builder for further configuration.</returns>
    public static ITemporalWorkerServiceOptionsBuilder AddTemporalWorker(
        this TemporalBuilder builder,
        string taskQueue,
        Action<TemporalWorkerServiceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Services.AddTemporalWorker(taskQueue, configure);
    }

    /// <summary>
    /// Registers a hosted Temporal worker that opts into worker versioning via
    /// <see cref="WorkerDeploymentOptions"/> (public preview).
    /// </summary>
    public static ITemporalWorkerServiceOptionsBuilder AddTemporalWorker(
        this TemporalBuilder builder,
        string taskQueue,
        WorkerDeploymentOptions deploymentOptions,
        Action<TemporalWorkerServiceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Services.AddTemporalWorker(taskQueue, deploymentOptions, configure);
    }

    /// <summary>
    /// Registers a hosted Temporal worker for the given task queue. No workflow
    /// or activity types are registered automatically — register them explicitly
    /// on the returned builder, or opt into auto-discovery with
    /// <see cref="TemporalWorkerDiscoveryExtensions.AddDiscoveredTypes"/>.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="taskQueue">Task queue the worker polls.</param>
    /// <param name="configure">Optional worker options configuration.</param>
    /// <returns>The underlying Temporal worker options builder for further configuration.</returns>
    public static ITemporalWorkerServiceOptionsBuilder AddTemporalWorker(
        this IServiceCollection services,
        string taskQueue,
        Action<TemporalWorkerServiceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(taskQueue);

        return AddTemporalWorkerCore(services, taskQueue, deploymentOptions: null, configure);
    }

    /// <summary>
    /// Registers a hosted Temporal worker that opts into worker versioning via
    /// <see cref="WorkerDeploymentOptions"/> (public preview).
    /// </summary>
    public static ITemporalWorkerServiceOptionsBuilder AddTemporalWorker(
        this IServiceCollection services,
        string taskQueue,
        WorkerDeploymentOptions deploymentOptions,
        Action<TemporalWorkerServiceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(taskQueue);
        ArgumentNullException.ThrowIfNull(deploymentOptions);

        if (deploymentOptions.Version is null)
        {
            throw new ArgumentException("Deployment version must be set when using worker versioning.", nameof(deploymentOptions));
        }

        return AddTemporalWorkerCore(services, taskQueue, deploymentOptions, configure);
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
            if (options.Metrics.UseDefaultInterceptor)
            {
                services.AddSingleton(sp =>
                {
                    var temporal = sp.GetRequiredService<IOptions<TemporalOptions>>().Value;
                    return new TemporalMetricsInterceptor(
                        sp.GetRequiredService<Meter>(),
                        temporal.Namespace,
                        temporal.Metrics.BaggageTagKeys);
                });
            }
        }

        if (options.Tracing.Enabled && options.Tracing.UseDefaultInterceptor)
        {
            services.AddSingleton(sp =>
            {
                var temporal = sp.GetRequiredService<IOptions<TemporalOptions>>().Value;
                return new BaggageTracingInterceptor(temporal.Tracing.BaggageTagKeys);
            });
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
                var interceptors = new List<IClientInterceptor>();
                if (options.Metrics.Enabled && options.Metrics.UseDefaultInterceptor
                    && sp.GetService<TemporalMetricsInterceptor>() is { } metrics)
                {
                    interceptors.Add(metrics);
                }

                if (options.Tracing.Enabled && options.Tracing.UseDefaultInterceptor
                    && sp.GetService<BaggageTracingInterceptor>() is { } tracing)
                {
                    interceptors.Add(tracing);
                }

                if (interceptors.Count > 0)
                {
                    connect.Interceptors = interceptors.ToArray();
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

            if (options.Metrics.Enabled && options.Metrics.UseDefaultInterceptor)
            {
                client.Configure<TemporalMetricsInterceptor>(AppendClientInterceptor);
            }

            if (options.Tracing.Enabled && options.Tracing.UseDefaultInterceptor)
            {
                client.Configure<BaggageTracingInterceptor>(AppendClientInterceptor);
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
        Action<TemporalWorkerServiceOptions>? configure)
    {
        var worker = services.AddHostedTemporalWorker(taskQueue, deploymentOptions);

        // Apply per-queue tuning from Temporal:Workers:<queue>. Registered before
        // the user's configure delegate so an explicit configure wins over the
        // appsettings values.
        worker.ConfigureOptions().Configure<IServiceProvider>((options, provider) =>
        {
            var temporal = provider.GetRequiredService<IOptions<TemporalOptions>>().Value;
            if (temporal.Workers is { } workers && workers.TryGetValue(taskQueue, out var tuning))
            {
                ApplyWorkerTuning(options, tuning);
            }
        });

        if (configure is not null)
        {
            worker.ConfigureOptions(configure);
        }

        return worker;
    }

    private static void ApplyWorkerTuning(TemporalWorkerServiceOptions options, TemporalWorkerTuningOptions tuning)
    {
        if (tuning.MaxConcurrentActivities is { } maxConcurrentActivities)
        {
            options.MaxConcurrentActivities = maxConcurrentActivities;
        }

        if (tuning.MaxConcurrentWorkflowTasks is { } maxConcurrentWorkflowTasks)
        {
            options.MaxConcurrentWorkflowTasks = maxConcurrentWorkflowTasks;
        }

        if (tuning.MaxConcurrentLocalActivities is { } maxConcurrentLocalActivities)
        {
            options.MaxConcurrentLocalActivities = maxConcurrentLocalActivities;
        }

        if (tuning.MaxConcurrentActivityTaskPolls is { } maxConcurrentActivityTaskPolls)
        {
            options.MaxConcurrentActivityTaskPolls = maxConcurrentActivityTaskPolls;
        }

        if (tuning.MaxConcurrentWorkflowTaskPolls is { } maxConcurrentWorkflowTaskPolls)
        {
            options.MaxConcurrentWorkflowTaskPolls = maxConcurrentWorkflowTaskPolls;
        }

        if (tuning.GracefulShutdownTimeout is { } gracefulShutdownTimeout)
        {
            options.GracefulShutdownTimeout = gracefulShutdownTimeout;
        }

        if (tuning.MaxCachedWorkflows is { } maxCachedWorkflows)
        {
            options.MaxCachedWorkflows = maxCachedWorkflows;
        }
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

    private static void AppendClientInterceptor<T>(TemporalClientConnectOptions connect, T interceptor)
        where T : IClientInterceptor
    {
        connect.Interceptors = (connect.Interceptors ?? Array.Empty<IClientInterceptor>())
            .Concat(new IClientInterceptor[] { interceptor })
            .ToArray();
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
        target.Tracing = source.Tracing;
        target.Logging = source.Logging;
        target.TestServer = source.TestServer;
        target.ConnectionWait = source.ConnectionWait;
        target.DataConverter = source.DataConverter;
        target.Workers = source.Workers;
    }
}
