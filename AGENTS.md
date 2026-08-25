# AGENTS.md

Kogoshvili.Temporal — static-analysis + library tooling for the Temporal .NET SDK (`Temporalio`). Eight NuGet packages + one CLI share the `Kogoshvili.Temporal` namespace. Not affiliated with Temporal Technologies.

## Build & test

- .NET SDK pinned in `global.json` (8.0.424, `rollForward: latestFeature`). Package versions are centrally managed in `Directory.Packages.props` (`ManagePackageVersionsCentrally`); vulnerable transitives are overridden there.
- `dotnet build` — `TreatWarningsAsErrors=true` in `Directory.Build.props`, so any warning fails the build.
- `dotnet test` — xunit. Run one test: `dotnet test --filter FullyQualifiedName~ClassName`.
- Full CI-equivalent sequence (`.github/workflows/ci.yml`): restore → `build -c Release` → `test --no-build -c Release` → build `samples/Temporal.SampleApp` (analyzer smoke test) → `dotnet pack` `src/Temporal.Analyzers` + `src/Temporal.Cli` into `artifacts/`.

## Architecture

Eight `src/` projects (two tools + six library packages):

- `Temporal.Analyzers` — Roslyn analyzer (**netstandard2.0**, must NOT reference the Temporal SDK). Rules in `Analyzers/*.cs`, metadata in `Diagnostics/DiagnosticDescriptors.cs`, fixes in `CodeFixes/`. Internals exposed to the CLI via `InternalsVisibleTo`.
- `Temporal.Cli` — `temporal-sharp` dotnet tool (net8.0, `PackAsTool`). Loads solutions via MSBuildWorkspace (`Analysis/`) and re-runs the same analyzers; needs `Microsoft.Build.Locator`. Commands: `analyze` (default), `map` (workflow topology → mermaid/json/html/dot), `history` (download workflow histories for later replay), `docs`, `preset`.
- `Temporal.Configuration` — shared connection config (net8.0): builds a `TemporalClient` from `appsettings.json` / `Temporal__*` env vars.
- `Temporal.Hosting` — generic-host worker starter over `Temporalio.Extensions.Hosting` (auto-discovery, metrics, test-server toggle, shared `DataConverter`).
- `Temporal.Codec` — composable `IPayloadCodec`s (encryption, claim-check, chains); no cloud deps.
- `Temporal.CodecServer` — ASP.NET Core library mapping `/encode`/`/decode` with JWT-bearer + OIDC auth.
- `Temporal.Cloud` — Azure/AWS credential resolution, Blob/S3 claim-check stores, and Azure Key Vault / AWS Secrets Manager TLS certificate sources.
- `Temporal.Testing` — replay/regression harness (`ReplayHarness`, `ReplayResult`, `Snapshot`) built on `WorkflowReplayer`.

Only `Temporal.Analyzers` and `Temporal.Cli` are packed/published by CI; the others are packable but not published. All are net8.0 except the analyzer.

Tests (eight projects under `tests/`):
- `Temporal.Analyzers.Tests` — Roslyn analyzer-testing framework; injects stub Temporal types by name from `TestStubs.cs` and never references the real SDK.
- `Temporal.Cli.Tests` — docs/preset generation, call graph, topology.
- `Temporal.{Configuration,Hosting,Testing}.Tests` — exercise the wrappers against the real `Temporalio` SDK.
- `Temporal.Codec.Tests` — codec round-trips and store behavior.
- `Temporal.CodecServer.Tests` — HTTP protocol, CORS, and auth via `TestHost`.
- `Temporal.Cloud.Tests` — PFX→PEM conversion and certificate-source behavior.

`samples/Temporal.SampleApp` intentionally violates rules; its `.editorconfig` downgrades every rule to `warning` so the smoke-test build succeeds. `samples/Temporal.HostingDemo.{Raw,Hosted}` are standalone examples outside CI.

## Rule catalog is generated — do not hand-edit

`src/Temporal.Analyzers/Diagnostics/DiagnosticDescriptors.cs` is the single source of truth. Derived artifacts, regenerated (never hand-edited):

- `RULES.md` — `dotnet run --project src/Temporal.Cli -- docs` (or `temporal-sharp docs`).
- `editorconfig/recommended.editorconfig` / `strict.editorconfig` — `temporal-sharp preset <recommended|strict>`.

`DocumentationSyncTests` and `ConsistencyTests` fail the build if descriptors, `RULES.md`, and analyzer `SupportedDiagnostics` drift. **Adding a rule requires four coordinated edits**: descriptor in `DiagnosticDescriptors.cs`, declaration in an analyzer's `SupportedDiagnostics`, an entry in `src/Temporal.Analyzers/AnalyzerReleases.Unshipped.md`, and a regenerated `RULES.md`. `docs.yml` auto-commits `RULES.md` on pushes to `main` touching `DiagnosticDescriptors.cs`, but run `docs` locally before committing descriptor changes.

## Conventions

- File-scoped namespaces enforced (`csharp_style_namespace_declarations = file_scoped:warning` in `.editorconfig` — an error under `TreatWarningsAsErrors`). 4-space indent, Allman braces.
- Rule IDs are `TMP####`, grouped by category prefix (TMP0xxx determinism, TMP1xxx shared state, TMP2xxx/TMP3xxx SDK misuse, TMP4xxx best practice, TMP5xxx testing). Opt-in rules set `isEnabledByDefault: false`.
- Versioning is MinVer, derived from git `v*` tags (`MinVerTagPrefix=v` in `Directory.Build.props`); commits must include full history (CI uses `fetch-depth: 0`).
- Suppression works via `#pragma warning disable TMPxxxx` or `.editorconfig` `dotnet_diagnostic.TMPxxxx.severity`; both apply in the analyzer and the CLI.

## Release

`release.yml` (manual, version input) updates `CHANGELOG.md` via `scripts/update-changelog.sh`, then commits+tags `v<version>`; `publish.yml` builds/tests/packs and pushes to NuGet (OIDC) on the tag. The changelog update and tag are one commit to keep MinVer's derived version correct.


<!-- headroom:memory-instructions -->
## Memory

Use the `headroom_memory` MCP server for persistent cross-session knowledge.

**Before** answering questions about prior decisions, conventions, project context,
architecture, user preferences, org info, codenames, debugging history, or anything
from past sessions — call `memory_search` (exposed to opencode as `headroom_memory_memory_search`) first.

**After** making durable decisions, discovering conventions, or learning important
facts — call `memory_save` (exposed to opencode as `headroom_memory_memory_save`) to persist them for future sessions.

Memory is your first source of truth for anything not visible in the current conversation.

## Headroom tool naming

OpenCode namespaces MCP tools as `<server>_<tool>`, so Headroom's tool names
differ from their bare names:

- `headroom_memory` server → `headroom_memory_memory_search` / `headroom_memory_memory_save` (bare: `memory_search` / `memory_save`).
- `headroom` compression server → `headroom_headroom_compress` / `headroom_headroom_retrieve` / `headroom_headroom_stats` (bare: `headroom_compress` / `headroom_retrieve` / `headroom_stats`; the "headroom" doubling is normal namespacing, not a bug).
