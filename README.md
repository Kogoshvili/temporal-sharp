# Kogoshvili.Temporal

A suite of static-analysis tooling and libraries for the
[Temporal](https://temporal.io) .NET SDK. It catches non-deterministic code and
SDK feature-misuse in your workflows before they hit production replay bugs, and
ships libraries for building and operating Temporal workers.

## Packages

Two tools, one rule engine:

- **[`Kogoshvili.Temporal.Analyzers`](src/Temporal.Analyzers/README.md)** — a
  Roslyn analyzer package that plugs into `dotnet build`, Visual Studio, and
  Rider.
- **[`Kogoshvili.Temporal.Cli`](src/Temporal.Cli/README.md)** — a standalone CLI
  (`dotnet tool`, invoked as `temporal-sharp`) for CI pipelines: `analyze`,
  `map`, `history`, `docs`, and `preset`.

Six libraries for building and operating Temporal workers:

- **[`Kogoshvili.Temporal.Hosting`](src/Temporal.Hosting/README.md)** — a
  generic-host worker starter: config binding, convention-based workflow/activity
  auto-discovery, a shared `DataConverter` (encryption + claim-check), metrics,
  and a test-server toggle.
- **[`Kogoshvili.Temporal.Codec`](src/Temporal.Codec/README.md)** — composable
  payload codecs (AES-GCM encryption, claim-check offloading, ordered chains,
  per-field `Secret<T>` encryption).
- **[`Kogoshvili.Temporal.CodecServer`](src/Temporal.CodecServer/README.md)** — a
  ready-made HTTP codec server for the Web UI/CLI, with JWT-bearer and OAuth2
  authorization-code auth.
- **[`Kogoshvili.Temporal.Cloud`](src/Temporal.Cloud/README.md)** — Azure/AWS
  credential resolution, Blob/S3 claim-check stores, and TLS certificate sources
  (Key Vault / Secrets Manager).
- **[`Kogoshvili.Temporal.Configuration`](src/Temporal.Configuration/README.md)** —
  shared connection config (loads a `TemporalClient` from `appsettings.json` /
  `Temporal__*` env vars).
- **[`Kogoshvili.Temporal.Testing`](src/Temporal.Testing/README.md)** — a
  replay/regression harness built on `WorkflowReplayer`.

See [`RULES.md`](RULES.md) for the full rule catalog, and
[`samples/`](samples/) for runnable demos of the hosting starter and codec
server.

## Templates

Install project templates for a ready-to-run start:

```sh
dotnet new install Kogoshvili.Temporal.Templates
dotnet new temporal-codec-server -o MyCodecServer   # a codec server for the Web UI/CLI
```

See [`templates/`](templates/) for the full template catalog.

## Install

```sh
dotnet add package Kogoshvili.Temporal.Analyzers   # analyzer, via NuGet
dotnet tool install -g Kogoshvili.Temporal.Cli     # CLI, invoked as `temporal-sharp`
```

Each package has its own README covering its configuration and usage — see the
links above.

## License

[MIT](LICENSE)

---

> Not affiliated with or endorsed by Temporal Technologies.
