# AGENTS.md

Kogoshvili.Temporal — a Roslyn analyzer package + `temporal-sharp` CLI for the Temporal .NET SDK. Two tools share one rule engine (`src/Temporal.Analyzers`). Not affiliated with Temporal Technologies.

## Build & test

- .NET SDK pinned in `global.json` (8.0.424, `rollForward: latestFeature`). Package versions are centrally managed in `Directory.Packages.props`.
- `dotnet build` — `TreatWarningsAsErrors=true` is set globally in `Directory.Build.props`, so any warning fails the build.
- `dotnet test` — xunit. Run one test: `dotnet test --filter FullyQualifiedName~ClassName`.
- Full CI-equivalent sequence (see `.github/workflows/ci.yml`): restore → `build -c Release` → `test --no-build -c Release` → build `samples/Temporal.SampleApp` (analyzer smoke test) → `dotnet pack` both src projects into `artifacts/`.

## Architecture

- `src/Temporal.Analyzers` — the analyzer (netstandard2.0). Rules live in `Analyzers/*.cs`; metadata in `Diagnostics/DiagnosticDescriptors.cs`. NuGet id `Kogoshvili.Temporal.Analyzers`.
- `src/Temporal.Cli` — `temporal-sharp` dotnet tool (net8.0, `PackAsTool`). Loads a solution via MSBuildWorkspace (`Analysis/`) and re-runs the same analyzers; needs `Microsoft.Build.Locator`.
- `tests/Temporal.Analyzers.Tests` + `tests/Temporal.Cli.Tests` — use the Roslyn analyzer-testing framework. Tests do **not** reference the real Temporal SDK; they inject stub types by name from `tests/Temporal.Analyzers.Tests/TestStubs.cs`.
- `samples/Temporal.SampleApp` — intentionally violates rules; its `.editorconfig` downgrades every rule to `warning` so the smoke-test build succeeds.

## Rule catalog is generated — do not hand-edit

`src/Temporal.Analyzers/Diagnostics/DiagnosticDescriptors.cs` is the single source of truth. Derived artifacts, all regenerated (never hand-edited):

- `RULES.md` — `dotnet run --project src/Temporal.Cli -- docs` (or `temporal-sharp docs`).
- `editorconfig/recommended.editorconfig` / `strict.editorconfig` — `temporal-sharp preset <recommended|strict>`.

`DocumentationSyncTests` and `ConsistencyTests` fail the build if descriptors, RULES.md, and analyzer `SupportedDiagnostics` drift. **Adding a rule requires four coordinated edits**: descriptor in `DiagnosticDescriptors.cs`, declaration in an analyzer's `SupportedDiagnostics`, an entry in `src/Temporal.Analyzers/AnalyzerReleases.Unshipped.md`, and a regenerated `RULES.md`. Run `docs` generation before committing descriptor changes.

## Conventions

- File-scoped namespaces are enforced (`csharp_style_namespace_declarations = file_scoped:warning` in `.editorconfig` — an error under `TreatWarningsAsErrors`). 4-space indent, Allman braces.
- Rule IDs are `TMP####`, grouped by category prefix (TMP0xxx determinism, TMP1xxx shared state, TMP2xxx/TMP3xxx SDK misuse, TMP4xxx best practice). Opt-in rules set `isEnabledByDefault: false`.
- Versioning is MinVer, derived from git `v*` tags (`MinVerTagPrefix=v` in `Directory.Build.props`); commits must include full history (CI uses `fetch-depth: 0`).
- Suppression works via `#pragma warning disable TMPxxxx` or `.editorconfig` `dotnet_diagnostic.TMPxxxx.severity`; both apply in the analyzer and the CLI.

## Release

`release.yml` (manual, version input) updates `CHANGELOG.md` via `scripts/update-changelog.sh`, tags `v<version>`, then `publish.yml` builds/tests/packs and pushes to NuGet on the tag. The changelog update and tag are the same commit to keep MinVer's derived version correct.
