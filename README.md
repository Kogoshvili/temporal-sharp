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

Opt-in rules (`TMP2103`, `TMP2111`, `TMP2151`, `TMP2161`, `TMP2171`)
are enabled via `.editorconfig`:

```ini
dotnet_diagnostic.TMP2103.severity = warning
dotnet_diagnostic.TMP2111.severity = warning
dotnet_diagnostic.TMP2151.severity = warning
dotnet_diagnostic.TMP2161.severity = warning
dotnet_diagnostic.TMP2171.severity = warning
```

Two rules take custom config keys:

- `kogoshvili.temporal.sensitive_pattern` (regex for `TMP2151` sensitive args).
- `kogoshvili.temporal.search_attributes` (alias=attribute map for `TMP2161`), e.g.:

```ini
[*.cs]
kogoshvili.temporal.search_attributes = user_id=user_id, client_id=user_id
```

## Roadmap

- [ ] **Code fixes**: `CodeFixProvider`s for high-value rules (e.g.
      `DateTime.Now` → `Workflow.UtcNow`, `Guid.NewGuid()` → `Workflow.NewGuid()`).

## Alternatives

Kogoshvili.Temporal covers the same ground as the Go ecosystem's tools, for .NET:

- **workflowcheck** — Temporal's first-party Go determinism analyzer
  (`github.com/temporalio/sdk-go/contrib/tools/workflowcheck`).
- **temporalcheck-lint** — a community Go type-safety/feature-misuse linter
  (`github.com/samgozman/temporalcheck-lint`).

## License

[MIT](LICENSE)

---

> Not affiliated with or endorsed by Temporal Technologies.
