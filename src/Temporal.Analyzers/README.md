# Kogoshvili.Temporal.Analyzers

A Roslyn analyzer that catches non-deterministic code and SDK feature-misuse in
your [Temporal](https://temporal.io) .NET SDK workflows before they hit
production replay bugs. It plugs into `dotnet build`, Visual Studio, and Rider.

See [`RULES.md`](../../RULES.md) for the full rule catalog, and the
[repository README](../../README.md) for the rest of the `Kogoshvili.Temporal`
suite.

## Install

```sh
dotnet add package Kogoshvili.Temporal.Analyzers
```

## Configuration

Suppress a single finding inline with `#pragma warning disable` / `restore`, or
disable a rule project-wide via `.editorconfig`
(`dotnet_diagnostic.TMPxxxx.severity = none`). Both mechanisms work in the
analyzer package and the CLI.

```csharp
#pragma warning disable TMP0101
var now = DateTime.Now;
#pragma warning restore TMP0101
```

Opt-in rules (`TMP2103`, `TMP2111`, `TMP2147`, `TMP2151`, `TMP2161`,
`TMP2171`, `TMP3104`, `TMP4104`, `TMP5001`, `TMP5002`, `TMP5003`) are enabled
via `.editorconfig`:

```ini
dotnet_diagnostic.TMP2103.severity = warning
dotnet_diagnostic.TMP2111.severity = warning
dotnet_diagnostic.TMP2147.severity = warning
dotnet_diagnostic.TMP2151.severity = warning
dotnet_diagnostic.TMP2161.severity = warning
dotnet_diagnostic.TMP2171.severity = warning
dotnet_diagnostic.TMP3104.severity = warning
dotnet_diagnostic.TMP4104.severity = warning
dotnet_diagnostic.TMP5001.severity = warning
dotnet_diagnostic.TMP5002.severity = warning
dotnet_diagnostic.TMP5003.severity = warning
```

Two rules take custom config keys:

- `kogoshvili.temporal.sensitive_pattern` (regex for `TMP2151` sensitive args).
- `kogoshvili.temporal.search_attributes` (alias=attribute map for `TMP2161`), e.g.:

```ini
[*.cs]
kogoshvili.temporal.search_attributes = user_id=user_id, client_id=user_id
```

Two more opt-in features are configured the same way:

- `kogoshvili.temporal.workflow_paths` — comma-separated path globs (e.g.
  `**/Workflows/**`) that treat files as workflow code even without a `[Workflow]`
  attribute, so rules fire for non-annotated helpers.
- `kogoshvili.temporal.unsafe_namespaces` — comma-separated namespace prefixes
  that workflow code must not import (`TMP2147`, off by default):

```ini
[*.cs]
kogoshvili.temporal.workflow_paths = **/Workflows/**
kogoshvili.temporal.unsafe_namespaces = System.IO, System.Net.Http
dotnet_diagnostic.TMP2147.severity = warning
```

## Severity presets

Two named presets are available as ready-to-copy `.editorconfig` bundles under
[`editorconfig/`](../../editorconfig/) — `recommended` (today's defaults) and
`strict` (every rule, including opt-in rules, promoted to `error`). Regenerate
them with `temporal-sharp preset`, or copy the block straight into your
`.editorconfig`.

## Alternatives

Kogoshvili.Temporal covers the same ground as the Go ecosystem's tools, for
.NET:

- **[workflowcheck](https://github.com/temporalio/sdk-go/tree/main/contrib/tools/workflowcheck)** — Temporal's first-party Go determinism analyzer.
- **[temporalcheck-lint](https://github.com/samgozman/temporalcheck-lint)** — a community Go type-safety/feature-misuse linter.
- **[eslint-plugin-temporal](https://github.com/stevekinney/eslint-plugin-temporal)** — a community JavaScript/TypeScript Temporal linter.

Not affiliated with or endorsed by Temporal Technologies.
