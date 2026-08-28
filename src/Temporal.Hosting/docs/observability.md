# Observability

Three independent opt-ins cover metrics, tracing, and Core log forwarding:
`Metrics:Enabled` records high-level client and activity metrics on a .NET
`Meter`, `Tracing:Enabled` wires the SDK's `TracingInterceptor` for spans, and
`Logging:Enabled` forwards the runtime's Core (Rust bridge) logs into your
`ILogger` pipeline.

## Minimal setup

Each aspect is a one-line opt-in. Enable all three:

```json
{
  "Temporal": {
    "Metrics": { "Enabled": true },
    "Tracing": { "Enabled": true },
    "Logging": { "Enabled": true }
  }
}
```

- `Metrics:Enabled` registers a `Meter` named `Temporal.Hosting` and a built-in
  interceptor that records `temporal.client.workflow.*` and
  `temporal.worker.activity.*` metrics on it. Nothing is exported until you
  subscribe a listener to that meter.
- `Tracing:Enabled` wires the SDK's `Temporalio.Extensions.OpenTelemetry.TracingInterceptor`
  onto the client and every worker. Spans are created via `ActivitySource` and
  only emitted when a listener (e.g. an OpenTelemetry tracer provider)
  subscribes to those sources.
- `Logging:Enabled` forwards Core logs into the `Temporalio.Core` logger
  category, where they respect the usual `Logging:LogLevel` filters. This uses
  the `ILoggerFactory` that a generic host already registers.

## Configuration

### Metrics

The section records custom metrics on the named meter and can additionally turn
on the Core SDK's own metric export:

```json
{
  "Temporal": {
    "Metrics": {
      "Enabled": true,
      "MeterName": "Temporal.Hosting",
      "UseDefaultInterceptor": true,
      "BaggageTagKeys": [],
      "PrometheusBindAddress": null,
      "OpenTelemetryUrl": null
    }
  }
}
```

- `Enabled` — record metrics via the built-in interceptor.
- `MeterName` (default `Temporal.Hosting`) — the `Meter` the custom metrics are
  recorded on.
- `UseDefaultInterceptor` (default `true`) — wire the built-in interceptor into
  the client and workers. Set to `false` to keep the meter but record your own
  metrics via the SDK's `Interceptors = [...]` option.
- `BaggageTagKeys` — allowlist of OpenTelemetry baggage keys whose values become
  `baggage.<key>` metric tags. Empty by default to avoid leaking arbitrary
  baggage into metric dimensions. On the worker side these tags only appear when
  tracing is also enabled, since the tracing interceptor propagates and restores
  baggage.
- `PrometheusBindAddress` / `OpenTelemetryUrl` — export the Core SDK's own
  metrics (the `temporal_*` set: client RPC counts/latency, workflow completion,
  activity latency, pollers, caches, slots). Set `PrometheusBindAddress` to
  e.g. `0.0.0.0:9000` to serve Prometheus metrics, or `OpenTelemetryUrl` to
  e.g. `http://localhost:4317` to forward them to an OTLP collector. When both
  are set, Prometheus wins. Either creates a dedicated `TemporalRuntime`.

The custom metrics on the meter are exported by subscribing a listener, e.g.
with OpenTelemetry:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddMeter("Temporal.Hosting").AddOtlpExporter());
```

### Tracing

```json
{
  "Temporal": {
    "Tracing": {
      "Enabled": true,
      "UseDefaultInterceptor": true,
      "BaggageTagKeys": []
    }
  }
}
```

- `Enabled` — wire the SDK's `TracingInterceptor`. Because it is also a worker
  interceptor, it applies to every worker automatically. Spans are created for
  client calls, workflows, and activities, with W3C trace context propagated
  through Temporal headers.
- `UseDefaultInterceptor` (default `true`) — use the built-in interceptor. Set
  to `false` to install your own via `Interceptors = [...]`.
- `BaggageTagKeys` — allowlisted baggage entries attached as `baggage.<key>`
  span attributes on client, workflow, and activity spans.

Spans are only emitted when something listens to the sources, so register them
with your tracer provider:

```csharp
using Temporalio.Extensions.OpenTelemetry;

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddSource(
            TracingInterceptor.ClientSource.Name,
            TracingInterceptor.WorkflowsSource.Name,
            TracingInterceptor.ActivitiesSource.Name)
        .AddConsoleExporter());
```

### Core log forwarding

```json
{
  "Temporal": {
    "Logging": {
      "Enabled": true,
      "Category": "Temporalio.Core"
    }
  }
}
```

- `Enabled` — forward the SDK runtime's Core logs into the application's
  `ILogger` pipeline.
- `Category` (default `Temporalio.Core`) — the logger category forwarded Core
  logs use, so they respect `Logging:LogLevel`:

```json
{
  "Logging": {
    "LogLevel": {
      "Temporalio.Core": "Debug"
    }
  }
}
```

## Full configuration

### Metrics recorded by the built-in interceptor

| Metric | Tags |
|---|---|
| `temporal.client.workflow.start.count/.duration` | `workflow.type`, `namespace`, `error` |
| `temporal.client.workflow.signal.count/.duration` | `workflow.id`, `signal`, `namespace`, `error` |
| `temporal.client.workflow.query.count/.duration` | `workflow.id`, `query`, `namespace`, `error` |
| `temporal.client.workflow.update.count/.duration` | `workflow.id`, `update`, `namespace`, `error` |
| `temporal.client.workflow.cancel.count/.duration` | `workflow.id`, `namespace`, `error` |
| `temporal.client.workflow.terminate.count/.duration` | `workflow.id`, `namespace`, `error` |
| `temporal.worker.activity.execution.count/.duration` | `activity.type`, `workflow.id`, `workflow.type`, `task.queue`, `namespace`, `error` |

The metrics interceptor is constructed per namespace (it tags metrics with the
client's namespace), while the tracing interceptor is namespace-agnostic and
shared. The custom metrics fill the tag gaps the Core SDK's own metrics do not
cover — notably `workflow.id` and allowlisted baggage.

### Runtime lifecycle

Enabling Core log forwarding or Core metric export constructs a dedicated
`TemporalRuntime`, which spawns its own Core thread pool. Log forwarding is
opt-in for this reason, and it throws at startup if `Temporal:Logging` is
enabled without an `ILoggerFactory` in the container (present by default in a
generic host). Tracing and the built-in metrics interceptor require no runtime;
the `LogLevel` filtering applied to forwarded logs happens downstream in the
`ILogger` pipeline.
