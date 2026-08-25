using System.Diagnostics;
using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.Hosting;
using Temporalio.Extensions.OpenTelemetry;

namespace Kogoshvili.Temporal.HostingDemo.Hosted;

/// <summary>
/// A tiny <see cref="ActivityListener"/> that prints the spans created by the
/// starter's tracing interceptor (<c>Temporal:Tracing:Enabled</c>) to the
/// console, so the tracing feature is visible without configuring a full
/// OpenTelemetry exporter. In production, subscribe the same
/// <see cref="TracingInterceptor"/> sources with a tracer provider instead.
/// </summary>
public sealed class TracePrinter : IHostedService
{
    private readonly ActivityListener listener = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        listener.ShouldListenTo = source =>
            source.Name == TracingInterceptor.ClientSource.Name
            || source.Name == TracingInterceptor.WorkflowsSource.Name
            || source.Name == TracingInterceptor.ActivitiesSource.Name;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData;
        listener.ActivityStarted = activity =>
            Console.WriteLine($"[trace] start {activity.OperationName}");
        listener.ActivityStopped = activity =>
            Console.WriteLine($"[trace] stop  {activity.OperationName} ({activity.Duration.TotalMilliseconds:0.##} ms)");
        ActivitySource.AddActivityListener(listener);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        listener.Dispose();
        return Task.CompletedTask;
    }
}
