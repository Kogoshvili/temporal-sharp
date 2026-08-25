using System.Diagnostics.Metrics;
using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Kogoshvili.Temporal.HostingDemo.Hosted;

/// <summary>
/// A tiny <see cref="MeterListener"/> that prints the client/activity metrics
/// recorded by the starter's built-in metrics interceptor
/// (<c>Metrics:Enabled</c>) to the console, so the metrics feature is visible
/// without Prometheus/OpenTelemetry.
/// </summary>
public sealed class MetricsPrinter : IHostedService
{
    private readonly MeterListener listener = new();
    private readonly string meterName;

    public MetricsPrinter(IOptions<TemporalOptions> options)
        => meterName = options.Value.Metrics.MeterName;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        listener.InstrumentPublished = (instrument, _) =>
        {
            if (instrument.Meter.Name == meterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>(PrintLong);
        listener.SetMeasurementEventCallback<double>(PrintDouble);
        listener.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        listener.Dispose();
        return Task.CompletedTask;
    }

    private static void PrintLong(
        Instrument instrument,
        long measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state) =>
        Console.WriteLine($"[metrics] {instrument.Name} = {measurement} {instrument.Unit}");

    private static void PrintDouble(
        Instrument instrument,
        double measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state) =>
        Console.WriteLine($"[metrics] {instrument.Name} = {measurement:0.##} {instrument.Unit}");
}
