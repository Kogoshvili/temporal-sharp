using System.Diagnostics.Metrics;
using Kogoshvili.Temporal.HostingDemo.Raw;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Extensions.Hosting;
using Temporalio.Worker;

var builder = Host.CreateApplicationBuilder(args);

// =========================================================================
// Everything below is what Kogoshvili.Temporal.Hosting collapses into
//
//     builder.Services.AddTemporal(builder.Configuration)
//         .AddTemporalWorker("raw-queue");
//
// No Kogoshvili.Temporal.Hosting extension is used here — only the raw SDK's
// own Temporalio / Temporalio.Extensions.Hosting building blocks.
// =========================================================================

// 1. Read connection settings by hand (AddTemporal(IConfiguration) does this).
var targetHost = builder.Configuration["Temporal:TargetHost"] ?? "localhost:7233";
var ns = builder.Configuration["Temporal:Namespace"] ?? "default";

// 2. (No in-process dev server here — this demo connects to a real server.
//    Start one first with `temporal server start-dev`. The starter can still run
//    an in-process dev server via Temporal:TestServer:Enabled = true.)

// 3. Register a metrics meter + hand-rolled interceptor (what Metrics:Enabled
//    gives you as TemporalMetricsInterceptor).
builder.Services.AddSingleton(_ => new Meter("Temporal.HostingDemo.Raw"));
builder.Services.AddSingleton<RawMetricsInterceptor>();

// 4. Register the client, attach the interceptor, and tune the RPC retry policy
//    (Temporal:RpcRetry in the starter's appsettings.json).
builder.Services.AddTemporalClient()
    .Configure(connect =>
    {
        connect.TargetHost = targetHost;
        connect.Namespace = ns;
        connect.RpcRetry = new RpcRetryOptions { MaxRetries = 5 };
    })
    .Configure<RawMetricsInterceptor>((connect, interceptor) =>
        connect.Interceptors = (connect.Interceptors ?? Array.Empty<IClientInterceptor>())
            .Concat(new IClientInterceptor[] { interceptor })
            .ToArray());

// 5. Wait for the server before workers poll (the starter's
//    TemporalConnectionWaiter, configured by Temporal:ConnectionWait).
builder.Services.AddHostedService<RawConnectionWaiter>();

// 6. Register every workflow and activity by hand, choosing each lifetime
//    explicitly. The starter's auto-discovery + [ActivityLifetime] do this.
builder.Services.AddHostedTemporalWorker("raw-queue", deploymentOptions: (WorkerDeploymentOptions?)null)
    .AddWorkflow<GreetingWorkflow>()
    .AddWorkflow<LifetimeProbeWorkflow>()
    .AddScopedActivities<ScopedActivities>()
    .AddSingletonActivities<SingletonActivities>()
    .AddTransientActivities<TransientActivities>()
    .AddStaticActivities(typeof(StaticActivities));

// 7. Self-start the demo workflows to prove the worker is live.
builder.Services.AddHostedService<DemoDriver>();

using var host = builder.Build();
await host.RunAsync();
