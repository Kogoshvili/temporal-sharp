#!/usr/bin/env bash
#
# Runs both Temporal.Hosting starter samples in test-server mode and shows a
# workflow executing end-to-end against an in-process Temporal dev server:
#   1. samples/Temporal.ConsoleWorker  (generic host, self-starts a workflow)
#   2. samples/Temporal.AspNetSample   (minimal API, workflows via HTTP)
#
# Prerequisites: .NET 8 SDK (pinned in global.json) and curl.
set -euo pipefail

export PATH="$HOME/.dotnet:$PATH"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$ROOT"

APP_URL="${APP_URL:-http://127.0.0.1:5080}"
LOG="$(mktemp -t temporal-hosting-demo.XXXXXX.log)"
PID=""

stop_app() {
    if [[ -n "${PID}" ]]; then
        # Signal the whole process group (app + in-process Temporal dev server),
        # giving the host a chance to dispose the test server gracefully.
        kill -TERM -"${PID}" 2>/dev/null || true
        local i
        for i in $(seq 1 20); do
            kill -0 "${PID}" 2>/dev/null || break
            sleep 0.5
        done
        kill -KILL -"${PID}" 2>/dev/null || true
        PID=""
    fi
}
trap 'stop_app; rm -f "${LOG}"' EXIT

echo "==> Building the solution (Release)..."
dotnet build Temporal.sln -c Release

# ---------------------------------------------------------------------------
# Demo 1: generic-host console worker
# ---------------------------------------------------------------------------
echo
echo "===== Demo 1: generic-host console worker (samples/Temporal.ConsoleWorker) ====="
echo "==> Starting the console worker (Temporal:TestServer:Enabled=true => in-process dev server)..."
cd "${ROOT}/samples/Temporal.ConsoleWorker"
setsid dotnet "bin/Release/net8.0/Temporal.ConsoleWorker.dll" >"${LOG}" 2>&1 &
PID=$!
cd "${ROOT}"

echo "==> Waiting for the self-started workflow to complete..."
for i in $(seq 1 180); do
    grep -q "Workflow result:" "${LOG}" 2>/dev/null && { echo "    done after ~${i}s"; break; }
    sleep 1
done

if ! grep -q "Workflow result:" "${LOG}" 2>/dev/null; then
    echo "ERROR: console worker did not produce a workflow result in time. Log:" >&2
    tail -n 40 "${LOG}" >&2
    exit 1
fi

echo "==> Console worker log:"
grep -E "Temporal test server started|Workflow result:" "${LOG}" || true
stop_app

# ---------------------------------------------------------------------------
# Demo 2: minimal API host
# ---------------------------------------------------------------------------
echo
echo "===== Demo 2: minimal API host (samples/Temporal.AspNetSample) ====="
echo "==> Starting the web API (test-server mode) on ${APP_URL}..."
cd "${ROOT}/samples/Temporal.AspNetSample"
ASPNETCORE_URLS="${APP_URL}" setsid dotnet "bin/Release/net8.0/Temporal.AspNetSample.dll" >"${LOG}" 2>&1 &
PID=$!
cd "${ROOT}"

echo "==> Waiting for the app (and its in-process Temporal test server) to come up..."
for i in $(seq 1 180); do
    curl -s -o /dev/null "${APP_URL}/" 2>/dev/null && { echo "    ready after ~${i}s"; break; }
    sleep 1
done

if ! curl -s -o /dev/null "${APP_URL}/" 2>/dev/null; then
    echo "ERROR: app did not become ready in time. Last log lines:" >&2
    tail -n 40 "${LOG}" >&2
    exit 1
fi

echo
echo "==> Test-server startup log:"
grep -E "Temporal test server" "${LOG}" || true

echo
echo "==> GET /"
curl -s "${APP_URL}/"
echo

echo
echo "==> POST /start/World (starts the auto-discovered workflow and awaits its result)"
curl -s -X POST "${APP_URL}/start/World"
echo

stop_app

echo
echo "==> Done. Both samples shut down."
