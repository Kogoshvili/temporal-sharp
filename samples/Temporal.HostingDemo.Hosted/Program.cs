using Kogoshvili.Temporal.CodecServer;
using Kogoshvili.Temporal.Hosting;
using Kogoshvili.Temporal.HostingDemo.Hosted;

var builder = WebApplication.CreateBuilder(args);

// The whole starter in one line: binds the "Temporal" section, wires the
// shared DataConverter (encryption + claim-check from Temporal:DataConverter),
// metrics, waits for the server (ConnectionWait), and registers every
// [Workflow]/[Activity] type in this assembly via opt-in auto-discovery
// (AddDiscoveredTypes) — assigning the four activity lifetimes by convention.
//
// For multiple queues, register types explicitly per worker instead:
//
//     builder.Services
//         .AddTemporalWorker("sql-queue").AddSingletonActivities<SqlActivities>()
//         .AddTemporalWorker("blob-queue").AddScopedActivities<BlobActivities>();
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
    .AddTemporalWorker("hosted-queue")
    .AddDiscoveredTypes();

// Host the codec server in the same app. It exposes /encode and /decode over
// HTTP, wrapping the *same* IPayloadCodec the client and workers use, so the
// Temporal Web UI / CLI can decrypt the payloads this worker writes.
//
//     temporal workflow show --workflow-id <id> --codec-endpoint http://localhost:5000
//
// Point the Web UI at http://localhost:5000 via the codec-server (eyeglasses)
// control. Uncomment the auth below to secure it for Temporal Cloud:
builder.Services.AddTemporalCodecServer(o =>
{
    // o.Auth.PassAccessToken = true;                 // validate the UI's JWT
    // o.Auth.IncludeCrossOriginCredentials = true;   // your own OAuth2 login flow
    // o.Auth.OidcAuthority = "https://login.example.com";
    // o.Auth.ClientId = "...";
    // o.Auth.ClientSecret = "...";
});

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
// Marker-type discovery (use when the entry assembly is not the worker assembly):
//   builder.Services.AddTemporalWorker("hosted-queue").AddDiscoveredTypes(typeof(GreetingWorkflow));
//
// Exporting the SDK's runtime metrics (set either of these in appsettings.json):
//   Temporal:Metrics:PrometheusBindAddress = "0.0.0.0:9000"
//   Temporal:Metrics:OpenTelemetryUrl      = "http://localhost:4317"
//
// Forwarding the SDK runtime's Core (Rust bridge) logs into this app's logger:
//   Temporal:Logging:Enabled = true (see appsettings.json). Core logs then flow
//   through the "Temporalio.Core" category and respect Logging:LogLevel.
//
// Per-queue worker tuning (see the Temporal:Workers section in appsettings.json):
//   Temporal:Workers:hosted-queue:MaxConcurrentActivities = 20
//   Temporal:Workers:hosted-queue:GracefulShutdownTimeout = 00:00:30

// Print the workflow-start metrics recorded by the interceptor (Metrics:Enabled).
builder.Services.AddHostedService<MetricsPrinter>();

// Self-start the demo workflows to prove the worker is live.
builder.Services.AddHostedService<DemoDriver>();

var app = builder.Build();

// CORS lets the browser (Temporal UI) call the codec server. Authentication
// middleware is only needed if you enabled it above.
app.UseCors();
// app.UseAuthentication();
// app.UseAuthorization();

app.MapTemporalCodecServer();

await app.RunAsync();
