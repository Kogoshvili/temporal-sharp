# Samples

Demo projects showing the `Kogoshvili.Temporal` libraries in action.

## Hosting demos

Two companion projects demonstrate the worker starter:

- **`Temporal.HostingDemo.Hosted`** — uses `Kogoshvili.Temporal.Hosting`
  (`AddTemporal` + `AddTemporalWorker`) with auto-discovery, metrics, an RPC
  retry policy, and startup connection-waiting, all bound from `appsettings.json`.
- **`Temporal.HostingDemo.Raw`** — the same app written against the raw
  `Temporalio` / `Temporalio.Extensions.Hosting` SDK, showing exactly what the
  starter collapses into (`RawConnectionWaiter`, hand-rolled interceptor, manual
  worker registration, `RpcRetryOptions`).

Both connect to a **real Temporal server** by default. Start one first:

```sh
temporal server start-dev
```

This runs a dev server with its frontend on `localhost:7233` and the web UI on
`http://localhost:8233`. Then run either demo in another terminal:

```sh
dotnet run --project samples/Temporal.HostingDemo.Hosted
dotnet run --project samples/Temporal.HostingDemo.Raw
```

Because `ConnectionWait` is enabled by default, you can start the app before the
server is up — it retries (with exponential backoff) until the server is
reachable, then the workers start polling.

### Running without a server (in-process dev server)

Prefer not to run `temporal server start-dev`? The starter can run an in-process
dev server instead. For the Hosted demo, set `Temporal:TestServer:Enabled` to
`true` (or export `Temporal__TestServer__Enabled=true`); `ConnectionWait` is
skipped automatically. The Raw demo has no such toggle and always needs a server.

### Key configuration (Hosted demo, `appsettings.json`)

| Key | Purpose |
| --- | --- |
| `Temporal:TargetHost` | Server `host:port` to connect to. |
| `Temporal:RpcRetry` | Connection-level RPC retry policy (intervals, multiplier, max retries/elapsed). |
| `Temporal:ConnectionWait` | Startup wait/retry before workers poll (`Enabled`, `Timeout`, `InitialDelay`, `MaxDelay`). |
| `Temporal:Metrics` | `System.Diagnostics.Metrics` meter + Prometheus/OTel export. |
| `Temporal:TestServer` | In-process dev server toggle (`Enabled`, `Port`). |
| `Temporal:Tls` | mTLS / server-root CA configuration. |

## Analyzer sample

- **`Temporal.SampleApp`** — intentionally violates the analyzer rules and is
  used as a smoke-test target in CI (its `.editorconfig` downgrades every rule to
  `warning`). Build it with `dotnet build samples/Temporal.SampleApp`.
