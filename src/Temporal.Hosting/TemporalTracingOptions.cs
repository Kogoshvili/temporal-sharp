namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Tracing configuration for the starter. When enabled, the SDK's
/// <see cref="Temporalio.Extensions.OpenTelemetry.TracingInterceptor"/> is
/// wired onto the client and (because it is also a worker interceptor)
/// automatically onto every worker. Spans are emitted through
/// <see cref="System.Diagnostics.ActivitySource"/>; the application must still
/// register those sources with an OpenTelemetry tracer provider (or other
/// <c>ActivityListener</c>) for anything to be exported.
/// </summary>
public sealed class TemporalTracingOptions
{
    /// <summary>Gets or sets a value indicating whether tracing is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the built-in tracing interceptor
    /// is wired into the client and workers. Set to <c>false</c> to install your
    /// own tracing interceptor via the SDK's <c>Interceptors</c> option instead.
    /// </summary>
    public bool UseDefaultInterceptor { get; set; } = true;

    /// <summary>
    /// Gets or sets the OpenTelemetry baggage keys whose values are attached as
    /// <c>baggage.&lt;key&gt;</c> attributes on the created spans. Empty by
    /// default; an explicit allowlist avoids leaking arbitrary baggage into
    /// traces.
    /// </summary>
    public string[] BaggageTagKeys { get; set; } = [];
}
