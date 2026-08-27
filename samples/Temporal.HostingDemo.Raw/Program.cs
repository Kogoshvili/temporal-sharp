using System.Diagnostics;
using System.Diagnostics.Metrics;
using Kogoshvili.Temporal.Codec;
using Kogoshvili.Temporal.HostingDemo.Raw;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Converters;
using Temporalio.Extensions.Hosting;
using Temporalio.Extensions.OpenTelemetry;
using Temporalio.Runtime;
using Temporalio.Worker;

var builder = Host.CreateApplicationBuilder(args);

// =========================================================================
// Everything below is what Kogoshvili.Temporal.Hosting collapses into
//
//     builder.Services.AddTemporal(builder.Configuration)
//         .AddTemporalWorker("raw-queue")
//         .AddDiscoveredTypes();
//
// No Kogoshvili.Temporal.Hosting extension is used here — only the raw SDK's
// own Temporalio / Temporalio.Extensions.Hosting building blocks (plus the
// Kogoshvili.Temporal.Codec codec library, which the starter drives from the
// Temporal:DataConverter configuration section).
// =========================================================================

// 1. Read connection settings by hand (AddTemporal(IConfiguration) does this).
var targetHost = builder.Configuration["Temporal:TargetHost"] ?? "localhost:7233";
var ns = builder.Configuration["Temporal:Namespace"] ?? "default";

// 2. (No in-process dev server here — this demo connects to a real server.
//    Start one first with `temporal server start-dev`. The starter can still run
//    an in-process dev server via Temporal:TestServer:Enabled = true.)

// 3. Register a metrics meter + hand-rolled interceptor (what Metrics:Enabled
//    gives you), and the SDK's tracing interceptor (what Tracing:Enabled gives
//    you).
builder.Services.AddSingleton(_ => new Meter("Temporal.HostingDemo.Raw"));
builder.Services.AddSingleton<RawMetricsInterceptor>();
builder.Services.AddSingleton<TracingInterceptor>();

// 4. Register the client, attach the interceptor, and tune the RPC retry policy
//    (Temporal:RpcRetry in the starter's appsettings.json).
//
//    Forward the SDK runtime's Core (Rust bridge) logs into this app's logger
//    — the starter's Temporal:Logging:Enabled does this by building a runtime
//    with LogForwardingOptions. Registering the runtime as a singleton shares
//    it between the client and every worker.
builder.Services.AddSingleton(sp =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    return new TemporalRuntime(new TemporalRuntimeOptions(new TelemetryOptions
    {
        Logging = new LoggingOptions
        {
            Forwarding = new LogForwardingOptions
            {
                Logger = loggerFactory.CreateLogger("Temporalio.Core"),
            },
        },
    }));
});

builder.Services.AddTemporalClient()
    .Configure(connect =>
    {
        connect.TargetHost = targetHost;
        connect.Namespace = ns;
        connect.RpcRetry = new RpcRetryOptions { MaxRetries = 5 };

        // Connection transport options — the starter binds these from
        // Temporal:KeepAlive / Temporal:HttpConnectProxy /
        // Temporal:DnsLoadBalancing / Temporal:GrpcCompression in appsettings.json.
        connect.KeepAlive = new KeepAliveOptions { Interval = TimeSpan.FromSeconds(30), Timeout = TimeSpan.FromSeconds(15) };
        // connect.HttpConnectProxy = new HttpConnectProxyOptions("proxy:8080");
        // connect.DnsLoadBalancing = new DnsLoadBalancingOptions { ResolutionInterval = TimeSpan.FromSeconds(30) };
        connect.GrpcCompression = new GrpcCompression.Gzip();

        // The starter's Temporal:DataConverter config. Here we build the same
        // thing by hand: encrypt every payload, then offload anything over the
        // threshold to a filesystem claim-check store. The single DataConverter
        // is set on the client, and workers inherit it.
        connect.DataConverter = DataConverter.Default with
        {
            PayloadCodec = new CompositePayloadCodec(
                new EncryptionCodec("test-key-test-key-test-key-test!", keyId: "demo"),
                new ClaimCheckCodec(new FileSystemClaimCheckStore("claim-check"), thresholdBytes: 512)),
        };
    })
    .Configure<TemporalRuntime>((connect, runtime) => connect.Runtime = runtime)
    .Configure<RawMetricsInterceptor>((connect, interceptor) =>
        connect.Interceptors = (connect.Interceptors ?? Array.Empty<IClientInterceptor>())
            .Concat(new IClientInterceptor[] { interceptor })
            .ToArray())
    .Configure<TracingInterceptor>((connect, interceptor) =>
        connect.Interceptors = (connect.Interceptors ?? Array.Empty<IClientInterceptor>())
            .Concat(new IClientInterceptor[] { interceptor })
            .ToArray());

// 5. Wait for the server before workers poll (the starter's
//    TemporalConnectionWaiter, configured by Temporal:ConnectionWait).
builder.Services.AddHostedService<RawConnectionWaiter>();

// 6. Register every workflow and activity by hand, choosing each lifetime
//    explicitly. The starter's opt-in AddDiscoveredTypes() + [ActivityLifetime]
//    do this for you.
builder.Services.AddHostedTemporalWorker("raw-queue", deploymentOptions: (WorkerDeploymentOptions?)null)
    .AddWorkflow<GreetingWorkflow>()
    .AddWorkflow<LifetimeProbeWorkflow>()
    .AddWorkflow<ClaimCheckWorkflow>()
    .AddWorkflow<SagaWorkflow>()
    .AddWorkflow<DownloadWorkflow>()
    .AddScopedActivities<ScopedActivities>()
    .AddSingletonActivities<SingletonActivities>()
    .AddTransientActivities<TransientActivities>()
    .AddStaticActivities(typeof(StaticActivities))
    .AddStaticActivities(typeof(ManualHeartbeatActivities));

// 7. Self-start the demo workflows to prove the worker is live.
builder.Services.AddHostedService<DemoDriver>();

// 8. Health checks — the starter's AddTemporalHealthChecks() + /health endpoint
//    wraps exactly this: verify the shared connection is serving, then describe
//    each task queue and check it has at least one poller (a connected worker).
//
//      var serving = await client.Connection.CheckHealthAsync();
//      var desc = await client.Connection.WorkflowService.DescribeTaskQueueAsync(
//          new DescribeTaskQueueRequest { Namespace = ns, TaskQueue = new TaskQueue { Name = "raw-queue" }, ReportPollers = true });
//      var healthy = serving && desc.Pollers.Count > 0;
//
//    A console host has no /health endpoint, so the result would be surfaced
//    through a HealthCheckService (Microsoft.Extensions.Diagnostics.HealthChecks)
//    instead of an HTTP route.

// 9. Observe the tracing interceptor's spans with a plain ActivityListener (the
//    starter's Tracing:Enabled wires the same TracingInterceptor). In production,
//    subscribe the sources with an OpenTelemetry tracer provider instead.
using var traceListener = new ActivityListener
{
    ShouldListenTo = source =>
        source.Name == TracingInterceptor.ClientSource.Name
        || source.Name == TracingInterceptor.WorkflowsSource.Name
        || source.Name == TracingInterceptor.ActivitiesSource.Name,
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
    SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
    ActivityStopped = activity =>
        Console.WriteLine($"[trace] {activity.OperationName} ({activity.Duration.TotalMilliseconds:0.##} ms)"),
};
ActivitySource.AddActivityListener(traceListener);

using var host = builder.Build();
await host.RunAsync();
