using System.Diagnostics.Metrics;
using Kogoshvili.Temporal.HostingDemo.Raw;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Extensions.Hosting;
using Temporalio.Testing;
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

// 2. Start an in-process dev server (what Temporal:TestServer:Enabled does).
//    The starter runs a hosted service that does this, then shares the resolved
//    ephemeral port with the lazy client automatically.
await using var environment = await WorkflowEnvironment.StartLocalAsync(
    new WorkflowEnvironmentStartLocalOptions { TargetHost = "127.0.0.1:0", Namespace = ns });
targetHost = environment.Client.Connection.Options.TargetHost;

// 3. Register a metrics meter + hand-rolled interceptor (what Metrics:Enabled
//    gives you as TemporalMetricsInterceptor).
builder.Services.AddSingleton(_ => new Meter("Temporal.HostingDemo.Raw"));
builder.Services.AddSingleton<RawMetricsInterceptor>();

// 4. Register the client and attach the interceptor manually.
builder.Services.AddTemporalClient()
    .Configure(connect =>
    {
        connect.TargetHost = targetHost;
        connect.Namespace = ns;
    })
    .Configure<RawMetricsInterceptor>((connect, interceptor) =>
        connect.Interceptors = (connect.Interceptors ?? Array.Empty<IClientInterceptor>())
            .Concat(new IClientInterceptor[] { interceptor })
            .ToArray());

// 5. Register every workflow and activity by hand, choosing each lifetime
//    explicitly. The starter's auto-discovery + [ActivityLifetime] do this.
builder.Services.AddHostedTemporalWorker("raw-queue", deploymentOptions: (WorkerDeploymentOptions?)null)
    .AddWorkflow<GreetingWorkflow>()
    .AddWorkflow<LifetimeProbeWorkflow>()
    .AddScopedActivities<ScopedActivities>()
    .AddSingletonActivities<SingletonActivities>()
    .AddTransientActivities<TransientActivities>()
    .AddStaticActivities(typeof(StaticActivities));

// 6. Self-start the demo workflows to prove the worker is live.
builder.Services.AddHostedService<DemoDriver>();

using var host = builder.Build();
await host.RunAsync();
