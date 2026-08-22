# TemporalSharp

A Roslyn static analyzer + CLI for the [Temporal](https://temporal.io) .NET SDK.
It catches non-deterministic code and SDK feature-misuse in your workflows before
they hit production replay bugs.

Two delivery vehicles, one rule engine:

- **`TemporalSharp.Analyzers`** — a NuGet analyzer package that plugs into
  `dotnet build`, Visual Studio, and Rider.
- **`temporal-sharp`** — a standalone CLI (`dotnet tool`) for CI pipelines, so
  findings stay visible in PRs even when a project already ignores warnings.

## Status

Early development. See [`RULES.md`](RULES.md) for the full rule catalog. All 29
rules are implemented. The CLI builds a solution-level call graph so a workflow
that calls a helper in another project is still checked for non-determinism.

## Install

```sh
dotnet add package TemporalSharp.Analyzers   # analyzer, via NuGet
dotnet tool install -g TemporalSharp.Cli     # CLI, invoked as `temporal-sharp`
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

Opt-in rules (`TMP2102`, `TMP2111`, `TMP2151`, `TMP2161`, `TMP2171`) are enabled
via `.editorconfig`:

```ini
dotnet_diagnostic.TMP2102.severity = warning
dotnet_diagnostic.TMP2111.severity = warning
dotnet_diagnostic.TMP2151.severity = warning
dotnet_diagnostic.TMP2161.severity = warning
dotnet_diagnostic.TMP2171.severity = warning
```

Two rules take custom config keys:

- `temporalsharp.sensitive_pattern` (regex for `TMP2151` sensitive args).
- `temporalsharp.search_attributes` (alias=attribute map for `TMP2161`), e.g.:

```ini
[*.cs]
temporalsharp.search_attributes = user_id=user_id, client_id=user_id
```

## Roadmap

- [ ] **Code fixes**: `CodeFixProvider`s for high-value rules (e.g.
      `DateTime.Now` → `Workflow.UtcNow`, `Guid.NewGuid()` → `Workflow.NewGuid()`).

## Alternatives

TemporalSharp covers the same ground as the Go ecosystem's tools, for .NET:

- **workflowcheck** — Temporal's first-party Go determinism analyzer
  (`github.com/temporalio/sdk-go/contrib/tools/workflowcheck`).
- **temporalcheck-lint** — a community Go type-safety/feature-misuse linter
  (`github.com/samgozman/temporalcheck-lint`).

## License

[MIT](LICENSE)

---

> Not affiliated with or endorsed by Temporal Technologies.
