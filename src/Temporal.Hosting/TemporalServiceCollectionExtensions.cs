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
using Kogoshvili.Temporal.Codec;

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
    /// Registers a Temporal client and starter services using a pre-built SDK
    /// client. This is the "configure everything yourself" escape hatch: the
    /// supplied client is used verbatim (no config-derived connection, data
    /// converter, or interceptors), and <see cref="ITemporalClientFactory"/> and
    /// the default <see cref="ITemporalClient"/> both return it regardless of
    /// namespace.
    /// </summary>
    public static TemporalBuilder AddTemporal(this IServiceCollection services, ITemporalClient client)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(client);

        var options = new TemporalOptions();
        services.Configure<TemporalOptions>(configured => CopyTo(options, configured));

        return RegisterCore(services, options, clientFactoryBuilder: _ => new StaticTemporalClientFactory(client));
    }

    /// <summary>
    /// Registers a Temporal client and starter services over a pre-built SDK
    /// connection. Namespace-scoped clients (and the default
    /// <see cref="ITemporalClient"/>) are fanned out over the supplied connection
    /// rather than a config-derived one; the hosting stack's data converter and
    /// interceptors still apply.
    /// </summary>
    public static TemporalBuilder AddTemporal(this IServiceCollection services, ITemporalConnection connection)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connection);

        var options = new TemporalOptions();
        services.Configure<TemporalOptions>(configured => CopyTo(options, configured));

        return RegisterCore(services, options, suppliedConnection: connection);
    }

    /// <summary>
    /// Registers a Temporal client and starter services using a client factory
    /// delegate. The delegate resolves the default <see cref="ITemporalClient"/>,
    /// which <see cref="ITemporalClientFactory"/> returns regardless of namespace.
    /// </summary>
    public static TemporalBuilder AddTemporal(
        this IServiceCollection services,
        Func<IServiceProvider, ITemporalClient> clientFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(clientFactory);

        var options = new TemporalOptions();
        services.Configure<TemporalOptions>(configured => CopyTo(options, configured));

        return RegisterCore(services, options, clientFactoryBuilder: sp => new DelegateTemporalClientFactory(clientFactory, sp));
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
    /// <param name="ns">
    /// Optional namespace the worker polls. Falls back to
    /// <c>Temporal:Workers:&lt;queue&gt;:Namespace</c>, then to the default
    /// namespace (<c>Temporal:Namespace</c>).
    /// </param>
    /// <param name="configure">Optional worker options configuration.</param>
    /// <returns>The underlying Temporal worker options builder for further configuration.</returns>
    public static ITemporalWorkerServiceOptionsBuilder AddTemporalWorker(
        this TemporalBuilder builder,
        string taskQueue,
        string? ns = null,
        Action<TemporalWorkerServiceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Services.AddTemporalWorker(taskQueue, ns, configure);
    }

    /// <summary>
    /// Registers a hosted Temporal worker that opts into worker versioning via
    /// <see cref="WorkerDeploymentOptions"/> (public preview).
    /// </summary>
    public static ITemporalWorkerServiceOptionsBuilder AddTemporalWorker(
        this TemporalBuilder builder,
        string taskQueue,
        string? ns,
        WorkerDeploymentOptions deploymentOptions,
        Action<TemporalWorkerServiceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Services.AddTemporalWorker(taskQueue, ns, deploymentOptions, configure);
    }

    /// <summary>
    /// Registers a hosted Temporal worker for the given task queue. No workflow
    /// or activity types are registered automatically — register them explicitly
    /// on the returned builder, or opt into auto-discovery with
    /// <see cref="TemporalWorkerDiscoveryExtensions.AddDiscoveredTypes"/>.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="taskQueue">Task queue the worker polls.</param>
    /// <param name="ns">
    /// Optional namespace the worker polls. Falls back to
    /// <c>Temporal:Workers:&lt;queue&gt;:Namespace</c>, then to the default
    /// namespace (<c>Temporal:Namespace</c>).
    /// </param>
    /// <param name="configure">Optional worker options configuration.</param>
    /// <returns>The underlying Temporal worker options builder for further configuration.</returns>
    public static ITemporalWorkerServiceOptionsBuilder AddTemporalWorker(
        this IServiceCollection services,
        string taskQueue,
        string? ns = null,
        Action<TemporalWorkerServiceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(taskQueue);

        return AddTemporalWorkerCore(services, taskQueue, ns, deploymentOptions: null, configure);
    }

    /// <summary>
    /// Registers a hosted Temporal worker that opts into worker versioning via
    /// <see cref="WorkerDeploymentOptions"/> (public preview).
    /// </summary>
    public static ITemporalWorkerServiceOptionsBuilder AddTemporalWorker(
        this IServiceCollection services,
        string taskQueue,
        string? ns,
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

        return AddTemporalWorkerCore(services, taskQueue, ns, deploymentOptions, configure);
    }

    private static TemporalBuilder RegisterCore(
        IServiceCollection services,
        TemporalOptions options,
        Func<IServiceProvider, ITemporalClientFactory>? clientFactoryBuilder = null,
        ITemporalConnection? suppliedConnection = null)
    {
        services.AddSingleton<IValidateOptions<TemporalOptions>, TemporalOptionsValidator>();

        // Register the bound snapshot so registration-time code (worker
        // deployment resolution) can read it synchronously. Deployment identity
        // is a registration-time concern and must not live-reload.
        services.AddSingleton(options);

        // Seed the static activity-options registry before any worker starts so
        // workflows can resolve presets deterministically during replay.
        SeedActivityOptionsRegistry(options.ActivityOptions);

        // Seed the static child-workflow-options registry from the same
        // Temporal:Workflows config so sandboxed workflows can resolve child
        // options (and the child ID convention) without DI.
        SeedChildWorkflowOptionsRegistry(options.Workflows);

        // Workflow-options resolution is client-side (DI-enabled callers), so it
        // is an injected singleton rather than a static registry.
        services.AddSingleton<WorkflowOptionsRegistry>();
        services.AddSingleton<IWorkflowOps, WorkflowOps>();
        services.AddSingleton<IScheduleOps, ScheduleOps>();
        services.AddSingleton<ISearchAttributeOps, SearchAttributeOps>();

        // Escape hatch: a caller-supplied SDK client/delegate replaces the entire
        // config-derived client stack (connection, data converter, interceptors,
        // test server, connection waiter). Everything before this point still
        // applies so worker registration and the ops facades keep working.
        if (clientFactoryBuilder is not null)
        {
            services.AddSingleton<ITemporalClientFactory>(clientFactoryBuilder);
            services.AddSingleton<ITemporalClient>(sp => sp.GetRequiredService<ITemporalClientFactory>().Get());
            return new TemporalBuilder(services);
        }

        var exportMetrics = !string.IsNullOrWhiteSpace(options.Metrics.PrometheusBindAddress)
            || !string.IsNullOrWhiteSpace(options.Metrics.OpenTelemetryUrl);
        var forwardLogs = options.Logging.Enabled;
        var needsRuntime = exportMetrics || forwardLogs;

        var payloadCodec = TemporalDataConverterFactory.BuildCodec(options.DataConverter);
        var hasCodec = options.DataConverter.Encryption.Enabled || options.DataConverter.ClaimCheck.Enabled;
        var dataConverter = payloadCodec is null
            ? DataConverter.Default
            : DataConverter.Default with { PayloadCodec = payloadCodec };

        if (options.Metrics.Enabled)
        {
            services.AddSingleton(sp => new Meter(sp.GetRequiredService<IOptions<TemporalOptions>>().Value.Metrics.MeterName));
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
        // exact same instance the client and workers encode/decode with. It is
        // read from the connect options (not captured here) so the secret loader
        // can swap in vault-backed codecs before anything resolves it.
        if (hasCodec)
        {
            services.AddSingleton<IPayloadCodec>(sp =>
                sp.GetRequiredService<TemporalClientConnectOptions>().DataConverter.PayloadCodec
                ?? throw new InvalidOperationException("No payload codec has been configured."));
        }

        // The per-field secret interceptor encrypts Secret<T> values on the client
        // and decrypts them on the activity worker. It resolves its key lazily (and
        // caches it) from the registered vault resolver selected by Source.
        if (options.DataConverter.Secret.Enabled)
        {
            services.AddSingleton(sp =>
            {
                var secret = sp.GetRequiredService<IOptions<TemporalOptions>>().Value.DataConverter.Secret;
                var resolver = sp.GetRequiredService<IEnumerable<ISecretResolver>>()
                    .FirstOrDefault(r => r.Name == secret.Source)
                    ?? throw new InvalidOperationException(
                        $"No secret resolver named '{secret.Source}' is registered. " +
                        "Register one via Kogoshvili.Temporal.Cloud (e.g. AddAzureKeyVaultSecretResolver).");

                return new SecretEncryptionInterceptor(resolver, secret.SecretId!, secret.KeyId, secret.Encoding);
            });
        }

        // A single connect-options instance carries both the connection settings
        // (host, TLS, API key, ...) and the client-level defaults (namespace, data
        // converter, logger factory, runtime). Registered as a singleton instance
        // (so the test server can fill in TargetHost and the factory can read it)
        // and as IOptions<TemporalClientConnectOptions> (so the certificate loader
        // can apply cloud TLS material before workers connect). All mutations
        // happen before the first client is resolved, so the shared lazy
        // connection sees finalized options.
        services.AddSingleton(sp =>
        {
            var connect = new TemporalClientConnectOptions { DataConverter = dataConverter };
            ClientOptionsFactory.Apply(connect, options);
            connect.LoggerFactory = sp.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            if (needsRuntime)
            {
                connect.Runtime = sp.GetRequiredService<TemporalRuntime>();
            }

            return connect;
        });
        services.AddSingleton<IOptions<TemporalClientConnectOptions>>(sp =>
            new OptionsWrapper<TemporalClientConnectOptions>(sp.GetRequiredService<TemporalClientConnectOptions>()));

        // Builds the client interceptor set for a namespace. The metrics
        // interceptor is per-namespace (it tags metrics with the client's
        // namespace), so it is constructed here rather than as a shared singleton.
        // The tracing interceptor is namespace-agnostic and shared.
        services.AddSingleton<Func<string, IReadOnlyCollection<IClientInterceptor>?>>(sp => ns =>
        {
            var temporal = sp.GetRequiredService<IOptions<TemporalOptions>>().Value;
            var interceptors = new List<IClientInterceptor>();
            if (temporal.Metrics.Enabled && temporal.Metrics.UseDefaultInterceptor
                && sp.GetService<Meter>() is { } meter)
            {
                interceptors.Add(new TemporalMetricsInterceptor(meter, ns, temporal.Metrics.BaggageTagKeys));
            }

            if (temporal.Tracing.Enabled && temporal.Tracing.UseDefaultInterceptor
                && sp.GetService<BaggageTracingInterceptor>() is { } tracing)
            {
                interceptors.Add(tracing);
            }

            if (temporal.DataConverter.Secret.Enabled
                && sp.GetService<SecretEncryptionInterceptor>() is { } secret)
            {
                interceptors.Add(secret);
            }

            return interceptors.Count == 0 ? null : interceptors;
        });

        services.AddSingleton<ITemporalClientFactory>(sp => new TemporalClientFactory(
            sp.GetRequiredService<TemporalClientConnectOptions>(),
            sp.GetRequiredService<Func<string, IReadOnlyCollection<IClientInterceptor>?>>(),
            suppliedConnection));
        services.AddSingleton<ITemporalClient>(sp => sp.GetRequiredService<ITemporalClientFactory>().Get());

        // Resolve vault-backed payload codec material (encryption key and cloud
        // claim-check store credentials) before the connection waiter or test
        // server starts, so the client's data converter is finalized before any
        // worker or registrar resolves the client.
        if ((options.DataConverter.Encryption.Enabled && options.DataConverter.Encryption.Source != "config")
            || (options.DataConverter.ClaimCheck.Enabled && options.DataConverter.ClaimCheck.Store != "filesystem"))
        {
            services.AddSingleton<IHostedService, TemporalSecretLoader>();
        }

        if (options.TestServer.Enabled)
        {
            services.AddSingleton<TemporalTestServerService>();
            services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<TemporalTestServerService>());
        }
        else
        {
            // Resolve cloud TLS certificate material before the connection waiter
            // connects (and therefore before any worker polls). Registered before
            // the waiter so hosted services start in the right order.
            services.AddSingleton<IHostedService, TemporalCertificateLoader>();

            // Wait for the server to be reachable before workers poll. Registered
            // here (during AddTemporal) so it starts before any worker service.
            services.AddSingleton<IHostedService, TemporalConnectionWaiter>();
        }

        // Registers declared schedules after the connection waiter / test server,
        // so the server is reachable before any schedule registration.
        services.AddSingleton<IHostedService, TemporalScheduleRegistrar>();

        // Registers declared search attributes after the connection waiter / test
        // server (and the schedule registrar), so the server is reachable before
        // any attribute is created.
        services.AddSingleton<IHostedService, SearchAttributeRegistrar>();

        return new TemporalBuilder(services);
    }

    private static ITemporalWorkerServiceOptionsBuilder AddTemporalWorkerCore(
        IServiceCollection services,
        string taskQueue,
        string? ns,
        WorkerDeploymentOptions? deploymentOptions,
        Action<TemporalWorkerServiceOptions>? configure)
    {
        GetOrAddWorkerTaskQueueRegistry(services).Register(taskQueue);

        // Resolve deployment/versioning eagerly: the (task queue, deployment
        // version) pair is the worker's unique identity and cannot be changed
        // later via ConfigureOptions. An explicit deploymentOptions argument
        // wins over the appsettings value.
        if (deploymentOptions is null
            && GetBoundTemporalOptions(services) is { } temporal
            && temporal.Workers is { } workers
            && workers.TryGetValue(taskQueue, out var workerConfig))
        {
            deploymentOptions = BuildWorkerDeploymentOptions(workerConfig.Deployment);
        }

        // Resolve the worker's namespace: explicit argument > appsettings
        // (Temporal:Workers:<queue>:Namespace) > default (Temporal:Namespace,
        // applied by the factory when ns is null).
        var resolvedNamespace = ns ?? GetBoundWorkerNamespace(services, taskQueue);

        // Register the worker options the same way the SDK's AddHostedTemporalWorker
        // does (named options keyed by task queue + version), but without its
        // hosted-service registration — the namespace-scoped client is bound here.
        var builder = new TemporalWorkerServiceOptionsBuilder(taskQueue, deploymentOptions, services);
        var worker = builder.ConfigureOptions(
            o =>
            {
                o.TaskQueue = taskQueue;
                o.DeploymentOptions = deploymentOptions;
            },
            disallowDuplicates: true);

        // Register the built-in workflow-settings local activity on every worker
        // so workflows can read Temporal:WorkflowSettings via WorkflowSettings.
        worker.AddSingletonActivities<WorkflowSettingsActivity>();

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

        // Bind the worker to its namespace's client (over the shared connection).
        // Runs after the connection waiter / test server / certificate loader, so
        // the client is resolvable and the connection finalized.
        var optionsName = GetWorkerOptionsName(taskQueue, deploymentOptions);
        services.AddSingleton<IHostedService>(sp =>
        {
            var client = sp.GetRequiredService<ITemporalClientFactory>().Get(resolvedNamespace);
            var options = sp.GetRequiredService<IOptionsMonitor<TemporalWorkerServiceOptions>>().Get(optionsName);
            return new TemporalWorkerService(client, options);
        });

        return worker;
    }

    private static void ApplyWorkerTuning(TemporalWorkerServiceOptions options, TemporalWorkerConfigOptions tuning)
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

    private static WorkerDeploymentOptions? BuildWorkerDeploymentOptions(
        TemporalWorkerDeploymentOptions? deployment)
    {
        if (deployment is null || !deployment.UseWorkerVersioning)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(deployment.DeploymentName))
        {
            throw new ArgumentException(
                "A deployment name must be set when worker versioning is enabled.",
                nameof(deployment));
        }

        var buildId = deployment.BuildId ?? deployment.Version;
        if (string.IsNullOrWhiteSpace(buildId))
        {
            throw new ArgumentException(
                "A build ID (BuildId or Version) must be set when worker versioning is enabled.",
                nameof(deployment));
        }

        return new WorkerDeploymentOptions(
            new WorkerDeploymentVersion(deployment.DeploymentName, buildId),
            useWorkerVersioning: true)
        {
            DefaultVersioningBehavior =
                deployment.DefaultVersioningBehavior ?? VersioningBehavior.Unspecified,
        };
    }

    private static TemporalOptions? GetBoundTemporalOptions(IServiceCollection services)
    {
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(TemporalOptions)
                && descriptor.ImplementationInstance is TemporalOptions options)
            {
                return options;
            }
        }

        return null;
    }

    private static string? GetBoundWorkerNamespace(IServiceCollection services, string taskQueue)
    {
        if (GetBoundTemporalOptions(services) is { } temporal
            && temporal.Workers is { } workers
            && workers.TryGetValue(taskQueue, out var workerConfig))
        {
            return workerConfig.Namespace;
        }

        return null;
    }

    // Mirrors the SDK's internal TemporalWorkerServiceOptions.GetUniqueOptionsName:
    // the worker options are keyed by task queue, or "taskQueue!!__temporal__!!version"
    // when a deployment version is set.
    private static string GetWorkerOptionsName(string taskQueue, WorkerDeploymentOptions? deploymentOptions)
    {
        var version = deploymentOptions?.Version?.ToCanonicalString();
        return version is null ? taskQueue : $"{taskQueue}!!__temporal__!!{version}";
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
        target.KeepAlive = source.KeepAlive;
        target.HttpConnectProxy = source.HttpConnectProxy;
        target.DnsLoadBalancing = source.DnsLoadBalancing;
        target.GrpcCompression = source.GrpcCompression;
        target.Metrics = source.Metrics;
        target.Tracing = source.Tracing;
        target.Logging = source.Logging;
        target.TestServer = source.TestServer;
        target.ConnectionWait = source.ConnectionWait;
        target.DataConverter = source.DataConverter;
        target.Workers = source.Workers;
        target.ActivityOptions = source.ActivityOptions;
        target.Workflows = source.Workflows;
        target.WorkflowSettings = source.WorkflowSettings;
        target.HealthChecks = source.HealthChecks;
        target.Schedules = source.Schedules;
        target.Namespaces = source.Namespaces;
    }

    private static void SeedActivityOptionsRegistry(TemporalActivityOptions? activityOptions)
    {
        if (activityOptions is null)
        {
            return;
        }

        var defaultOptions = ActivityOptionsFactory.Build(activityOptions.Default);
        var localDefaultOptions = LocalActivityOptionsFactory.Build(activityOptions.LocalDefault);
        var presets = new Dictionary<string, Temporalio.Workflows.ActivityOptions>(StringComparer.Ordinal);
        var localPresets = new Dictionary<string, Temporalio.Workflows.LocalActivityOptions>(StringComparer.Ordinal);

        if (activityOptions.Presets is { } named)
        {
            foreach (var (name, preset) in named)
            {
                if (ActivityOptionsFactory.Build(preset) is { } options)
                {
                    presets[name] = options;
                }

                if (LocalActivityOptionsFactory.Build(preset) is { } localOptions)
                {
                    localPresets[name] = localOptions;
                }
            }
        }

        ActivityOptionsRegistry.Replace(defaultOptions, presets, localDefaultOptions, localPresets);
    }

    private static void SeedChildWorkflowOptionsRegistry(TemporalWorkflowOptions? workflows)
    {
        ChildWorkflowOptionsRegistry.Replace(
            workflows?.Default,
            workflows?.ByType,
            workflows?.Id?.ChildFormat);
    }

    private static TemporalWorkerTaskQueueRegistry GetOrAddWorkerTaskQueueRegistry(IServiceCollection services)
    {
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(TemporalWorkerTaskQueueRegistry)
                && descriptor.ImplementationInstance is TemporalWorkerTaskQueueRegistry registry)
            {
                return registry;
            }
        }

        var created = new TemporalWorkerTaskQueueRegistry();
        services.AddSingleton(created);
        return created;
    }
}
