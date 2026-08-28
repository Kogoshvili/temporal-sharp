# Kogoshvili.Temporal.Analyzers

A Roslyn analyzer that catches non-deterministic code and Temporal SDK
feature-misuse in .NET workflows before they hit production replay bugs. It
runs in `dotnet build`, Visual Studio, and Rider with sensible defaults and no
configuration.

See [`RULES.md`](../../RULES.md) for the full rule catalog, and the
[repository README](../../README.md) for the rest of the `Kogoshvili.Temporal`
suite.

## Minimal setup

Add the package and build. Every rule is enabled at its built-in default
severity (mostly `error` and `warning`); no `.editorconfig` entry is required.

```sh
dotnet add package Kogoshvili.Temporal.Analyzers
```

```sh
dotnet build
```

```csharp
[Workflow]
public class GreetingWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        var now = DateTime.Now; // TMP0101: use Workflow.UtcNow instead
        return $"Hello, {name} at {now}";
    }
}
```

Opt-in rules stay off until you enable them, so a default build reports only
the high-signal findings.

## Configuration

All tuning happens through `.editorconfig`. Suppress a single finding inline
with `#pragma warning disable` / `restore`, or change a rule's severity
project-wide with `dotnet_diagnostic.TMPxxxx.severity`. Both mechanisms work in
the analyzer package and the CLI.

```csharp
#pragma warning disable TMP0101
var now = DateTime.Now;
#pragma warning restore TMP0101
```

```ini
[*.cs]
dotnet_diagnostic.TMP0101.severity = suggestion
```

Eleven rules are opt-in and ship disabled: `TMP2103`, `TMP2111`, `TMP2147`,
`TMP2151`, `TMP2161`, `TMP2171`, `TMP3104`, `TMP4104`, `TMP5001`, `TMP5002`,
and `TMP5003`. Enable the ones you want by setting their severity:

```ini
[*.cs]
dotnet_diagnostic.TMP2103.severity = warning
dotnet_diagnostic.TMP2147.severity = warning
dotnet_diagnostic.TMP5001.severity = warning
```

Four custom config keys drive individual rules. Two take arbitrary values:

- `kogoshvili.temporal.sensitive_pattern` — regex for `TMP2151` sensitive
  arguments. Defaults to
  `(?i)(password|passwd|secret|token|apikey|api_key|credential|connectionstring)`.
- `kogoshvili.temporal.search_attributes` — comma-separated `alias=attribute`
  map for `TMP2161`:

```ini
[*.cs]
kogoshvili.temporal.search_attributes = user_id=user_id, client_id=user_id
```

Two more opt-in keys extend which code is analyzed:

- `kogoshvili.temporal.workflow_paths` — comma-separated path globs (e.g.
  `**/Workflows/**`) that treat files as workflow code even without a
  `[Workflow]` attribute, so rules fire for non-annotated helpers.
- `kogoshvili.temporal.unsafe_namespaces` — comma-separated namespace prefixes
  that workflow code must not import (`TMP2147`):

```ini
[*.cs]
kogoshvili.temporal.workflow_paths = **/Workflows/**
kogoshvili.temporal.unsafe_namespaces = System.IO, System.Net.Http
dotnet_diagnostic.TMP2147.severity = warning
```

## Full configuration

Two named presets are available as ready-to-copy `.editorconfig` bundles under
[`editorconfig/`](../../editorconfig/):

- `recommended` — today's built-in defaults.
- `strict` — every rule, including opt-in rules, promoted to `error`.

Regenerate them with the CLI (`temporal-sharp preset <recommended|strict>`) or
copy the block straight into your `.editorconfig`. For example, adopting
`strict`:

```sh
dotnet tool install -g temporal-sharp
temporal-sharp preset strict > .editorconfig
```

The full set of `.editorconfig` knobs is the union of per-rule severities
(`dotnet_diagnostic.TMPxxxx.severity`) and the four custom keys above. Prefer
the presets as a starting point, then override individual severities rather
than hand-writing the whole list.

## Alternatives

Kogoshvili.Temporal covers the same ground as the Go ecosystem's tools, for
.NET:

- **[workflowcheck](https://github.com/temporalio/sdk-go/tree/main/contrib/tools/workflowcheck)** — Temporal's first-party Go determinism analyzer.
- **[temporalcheck-lint](https://github.com/samgozman/temporalcheck-lint)** — a community Go type-safety/feature-misuse linter.
- **[eslint-plugin-temporal](https://github.com/stevekinney/eslint-plugin-temporal)** — a community JavaScript/TypeScript Temporal linter.

Not affiliated with or endorsed by Temporal Technologies.
