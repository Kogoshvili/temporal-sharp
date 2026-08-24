#!/usr/bin/env bash
set -euo pipefail

# The repo pins the .NET SDK via global.json (8.0.424). Make sure the locally
# installed SDK is on PATH if it is not already available.
if ! command -v dotnet >/dev/null 2>&1; then
    export PATH="$HOME/.dotnet:$PATH"
fi

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$ROOT"

echo "==> .NET SDK"
dotnet --version
echo

echo "==> Build the solution (analyzers, CLI, and the Temporal.Testing harness)"
dotnet build Temporal.sln
echo

echo "==> Run the replay/regression harness tests"
echo "    (starts a time-skipping WorkflowEnvironment, runs a [Workflow] to"
echo "     completion, snapshots its history, and replays it via WorkflowReplayer)"
dotnet test tests/Temporal.Testing.Tests/Temporal.Testing.Tests.csproj
echo

echo "==> Demonstrate TMP5001 firing"
echo "    (a [Workflow] with no WorkflowReplayer replay test; TMP5001 is opt-in,"
echo "     so it is enabled here via --severity)"
dotnet run --project src/Temporal.Cli -- analyze \
    demo/ReplaylessWorkflow/ReplaylessWorkflow.csproj \
    --severity TMP5001=warning
echo

echo "==> Same project without --severity (TMP5001 is off by default -> no output)"
dotnet run --project src/Temporal.Cli -- analyze \
    demo/ReplaylessWorkflow/ReplaylessWorkflow.csproj
echo

echo "Demo complete."
