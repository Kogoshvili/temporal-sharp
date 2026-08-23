# Kogoshvili.Temporal

A suite of static-analysis tooling for the [Temporal](https://temporal.io) .NET SDK.
It catches non-deterministic code and SDK feature-misuse in your workflows before
they hit production replay bugs.

## Tools

Two tools, one rule engine:

- **`Kogoshvili.Temporal.Analyzers`** — a NuGet analyzer package that plugs into
  `dotnet build`, Visual Studio, and Rider.
- **`Kogoshvili.Temporal.Cli`** — a standalone CLI (`dotnet tool`, invoked as
  `temporal-sharp`) for CI pipelines, so findings stay visible in PRs even when
  a project already ignores warnings.

The suite is intentionally a namespace (`Kogoshvili.Temporal.*`) so more tools
can be added alongside the analyzer and CLI (e.g. `dotnet new` templates or a
source generator) without a naming collision.

See [`RULES.md`](RULES.md) for the full rule catalog.

## Install

```sh
dotnet add package Kogoshvili.Temporal.Analyzers   # analyzer, via NuGet
dotnet tool install -g Kogoshvili.Temporal.Cli     # CLI, invoked as `temporal-sharp`
```

## CLI

```
temporal-sharp analyze <path.sln|path.csproj> [options]
  --format <console|json|sarif>          Output format (default: console).
  --fail-on <none|info|warning|error>    Exit non-zero on findings at or above the given severity.
  --severity <TMPxxxx=severity>          Override a rule's severity (repeatable).

temporal-sharp preset <recommended|strict> [--write <file>]
                                         Emit an .editorconfig severity block for a preset.
```

### Severity presets

Two named presets are available as ready-to-copy `.editorconfig` bundles under
[`editorconfig/`](editorconfig/) — `recommended` (today's defaults) and `strict`
(every rule, including opt-in rules, promoted to `error`). Regenerate them with
`temporal-sharp preset`, or copy the block straight into your `.editorconfig`.

### GitHub Actions

Run `temporal-sharp` in CI as a pull-request gate, and optionally upload SARIF
so findings appear as GitHub code-scanning annotations:

```yaml
name: temporal-sharp

on:
  pull_request:
  push:
    branches: [main]

jobs:
  analyze:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Install temporal-sharp
        run: dotnet tool install --global Kogoshvili.Temporal.Cli

      - name: Run temporal-sharp
        run: |
          export PATH="$PATH:$HOME/.dotnet/tools"
          temporal-sharp analyze ./MyApp.sln --fail-on warning

      # Optional: emit SARIF and upload for GitHub code scanning.
      - name: Run temporal-sharp (SARIF)
        run: |
          export PATH="$PATH:$HOME/.dotnet/tools"
          temporal-sharp analyze ./MyApp.sln --format sarif > temporal.sarif

      - name: Upload SARIF
        uses: github/codeql-action/upload-sarif@v3
        with:
          sarif_file: temporal.sarif
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
`TMP2171`, `TMP4104`, `TMP4201`, `TMP4202`, `TMP4203`) are enabled via
`.editorconfig`:

```ini
dotnet_diagnostic.TMP2103.severity = warning
dotnet_diagnostic.TMP2111.severity = warning
dotnet_diagnostic.TMP2147.severity = warning
dotnet_diagnostic.TMP2151.severity = warning
dotnet_diagnostic.TMP2161.severity = warning
dotnet_diagnostic.TMP2171.severity = warning
dotnet_diagnostic.TMP4104.severity = warning
dotnet_diagnostic.TMP4201.severity = warning
dotnet_diagnostic.TMP4202.severity = warning
dotnet_diagnostic.TMP4203.severity = warning
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

## Alternatives

Kogoshvili.Temporal covers the same ground as the Go ecosystem's tools, for .NET:

- **workflowcheck** — Temporal's first-party Go determinism analyzer
  (`github.com/temporalio/sdk-go/contrib/tools/workflowcheck`).
- **temporalcheck-lint** — a community Go type-safety/feature-misuse linter
  (`github.com/samgozman/temporalcheck-lint`).
- **eslint-plugin-temporal** — the de-facto standard JavaScript/TypeScript Temporal
  linter (`github.com/stevekinney/eslint-plugin-temporal`).

## License

[MIT](LICENSE)

---

> Not affiliated with or endorsed by Temporal Technologies.
