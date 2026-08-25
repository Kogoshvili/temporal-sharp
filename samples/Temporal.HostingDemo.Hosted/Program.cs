using Kogoshvili.Temporal.Hosting;
using Kogoshvili.Temporal.HostingDemo.Hosted;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// The whole starter in one line: binds the "Temporal" section, wires metrics,
// waits for the server (ConnectionWait), and auto-discovers every
// [Workflow]/[Activity] type in this assembly — assigning the four activity
// lifetimes by convention.
//
// This demo connects to a real server. Start one first:
//
//     temporal server start-dev          # frontend :7233, UI :8233
//
// then run this project. ConnectionWait makes the starter retry (with backoff)
// until the server is reachable, so you can start the server after the app.
// RpcRetry in appsettings.json tunes the connection-level retry policy.
builder.Services
    .AddTemporal(builder.Configuration)
    .AddTemporalWorker("hosted-queue");

// Other features, shown for reference rather than run:
//
// In-process dev server (no external server needed; ConnectionWait is skipped):
//   Set Temporal:TestServer:Enabled = true (or Temporal__TestServer__Enabled=true).
//
// Worker versioning (public preview; the in-process dev server does not
// support deployments):
//   using Temporalio.Common;
//   using Temporalio.Worker;
//   builder.Services.AddTemporalWorker(
//       "hosted-queue",
//       new WorkerDeploymentOptions(new WorkerDeploymentVersion("hosted-app", "1.0"), useWorkerVersioning: true));
//
// Marker-type overload (use when the entry assembly is not the worker assembly):
//   builder.Services.AddTemporalWorker("hosted-queue", typeof(GreetingWorkflow));
//
// Exporting the SDK's runtime metrics (set either of these in appsettings.json):
//   Temporal:Metrics:PrometheusBindAddress = "0.0.0.0:9000"
//   Temporal:Metrics:OpenTelemetryUrl      = "http://localhost:4317"

// Print the workflow-start metrics recorded by the interceptor (Metrics:Enabled).
builder.Services.AddHostedService<MetricsPrinter>();

// Self-start the demo workflows to prove the worker is live.
builder.Services.AddHostedService<DemoDriver>();

using var host = builder.Build();
await host.RunAsync();
