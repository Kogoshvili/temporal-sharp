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
[`plan.md`](plan.md) for the implementation plan.

## License

[MIT](LICENSE)
