using Kogoshvili.Temporal.Hosting;
using Kogoshvili.Temporal.HostingDemo.Minimal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddTemporal(builder.Configuration)
    .AddTemporalWorker("minimal-queue")
    .AddDiscoveredTypes();

builder.Services.AddHostedService<DemoDriver>();

await builder.Build().RunAsync();
