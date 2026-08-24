#!/usr/bin/env bash
#
# Demo for the `temporal-sharp map` POC: builds the CLI and the sample app,
# then runs `map` against samples/Temporal.SampleApp in all four formats
# (mermaid, json, html, dot) plus a multi-input run, printing labelled results
# and refreshing the checked-in examples under docs/examples/.
set -euo pipefail

# Resolve the repository root (this script lives under docs/poc/map).
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$ROOT"

# The .NET SDK may be installed under ~/.dotnet and not on PATH.
if [[ -x "$HOME/.dotnet/dotnet" ]]; then
    export PATH="$HOME/.dotnet:$PATH"
fi

if ! command -v dotnet >/dev/null 2>&1; then
    echo "error: dotnet not found (looked on PATH and ~/.dotnet)" >&2
    exit 1
fi

RUN=(dotnet run --project src/Temporal.Cli -c Release --no-build -- map)

echo "==> Building the CLI"
dotnet build src/Temporal.Cli/Temporal.Cli.csproj -c Release

echo
echo "==> Building the sample app"
dotnet build samples/Temporal.SampleApp/Temporal.SampleApp.csproj -c Release

echo
echo "==> map samples/Temporal.SampleApp --format mermaid"
"${RUN[@]}" samples/Temporal.SampleApp --format mermaid

echo
echo "==> map samples/Temporal.SampleApp --format json"
"${RUN[@]}" samples/Temporal.SampleApp --format json

echo
echo "==> map samples/Temporal.SampleApp --format html  (docs/examples/topology.html)"
"${RUN[@]}" samples/Temporal.SampleApp --format html --output docs/examples/topology.html

echo
echo "==> map samples/Temporal.SampleApp --format dot  (docs/examples/topology.dot)"
"${RUN[@]}" samples/Temporal.SampleApp --format dot --output docs/examples/topology.dot
echo "--- topology.dot preview ---"
sed -n '1,8p' docs/examples/topology.dot

echo
echo "==> map (multi-input) samples/Temporal.SampleApp + Temporal.sln  (docs/examples/multi-input-topology.json)"
"${RUN[@]}" samples/Temporal.SampleApp/Temporal.SampleApp.csproj Temporal.sln \
    --format json --output docs/examples/multi-input-topology.json

echo
echo "==> Refreshing docs/examples/"
"${RUN[@]}" samples/Temporal.SampleApp --format mermaid --output docs/examples/sample-app-topology.mmd
"${RUN[@]}" samples/Temporal.SampleApp --format json --output docs/examples/sample-app-topology.json

echo
echo "==> Done."
