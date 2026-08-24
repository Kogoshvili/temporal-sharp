using Kogoshvili.Temporal.ConsoleWorker;
using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Same one-line startup as the web sample, but on a plain generic host: client +
// hosted worker with auto-discovery of [Workflow]/[Activity] types.
builder.Services
    .AddTemporal(builder.Configuration)
    .AddTemporalWorker("console-queue");

// Self-demonstrating: start one workflow shortly after startup and log the result.
builder.Services.AddHostedService<WorkerDemoService>();

using var host = builder.Build();
await host.RunAsync();
