# TemporalSharp

A Roslyn static analyzer + CLI for the [Temporal](https://temporal.io) .NET SDK.
It catches non-deterministic code and SDK feature-misuse in your workflows before
they hit production replay bugs.

> Not affiliated with or endorsed by Temporal Technologies.

Two delivery vehicles, one rule engine:

- **`TemporalSharp.Analyzers`** — a NuGet analyzer package that plugs into
  `dotnet build`, Visual Studio, and Rider.
- **`temporal-sharp`** — a standalone CLI (`dotnet tool`) for CI pipelines, so
  findings stay visible in PRs even when a project already ignores warnings.

## Status

Early development. See [`RULES.md`](RULES.md) for the full rule catalog and
[`plan.md`](plan.md) for the implementation plan. v1 and v2 rules are
implemented; v3 rules are pending.

## CLI

```
temporal-sharp analyze <path.sln|path.csproj> [options]
  --format <console|json|sarif>          Output format (default: console).
  --fail-on <none|info|warning|error>    Exit non-zero on findings at or above the given severity.
  --severity <TMPxxxx=severity>          Override a rule's severity (repeatable).
```

Suppress a finding with a `// workflowcheck:ignore` comment on the line or the
line immediately above the violation. Opt-in rules (e.g. `TMP2102`, `TMP2151`)
are enabled via `.editorconfig`:

```ini
dotnet_diagnostic.TMP2102.severity = warning
dotnet_diagnostic.TMP2151.severity = warning
```

## License

[MIT](LICENSE)
