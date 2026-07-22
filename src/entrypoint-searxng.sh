#!/bin/bash
set -euo pipefail

SEARXNG_PID=""
INFERPAGE_PID=""

shutdown() {
    if [[ -n "$SEARXNG_PID" ]]; then
        kill "$SEARXNG_PID" 2>/dev/null || true
    fi
    if [[ -n "$INFERPAGE_PID" ]]; then
        kill "$INFERPAGE_PID" 2>/dev/null || true
    fi
}

trap shutdown SIGTERM SIGINT

echo "[entrypoint] Starting SearXNG..."
/opt/searxng-venv/bin/python -m searx.webapp &
SEARXNG_PID=$!

echo "[entrypoint] Waiting for SearXNG..."
until bash -c 'printf "" 2>/dev/null >> /dev/tcp/127.0.0.1/8080' 2>/dev/null; do
    if ! kill -0 "$SEARXNG_PID" 2>/dev/null; then
        wait "$SEARXNG_PID"
        exit $?
    fi
    sleep 1
done

echo "[entrypoint] SearXNG is ready. Starting InferPage..."
dotnet MaIN.InferPage.dll &
INFERPAGE_PID=$!

EXIT_CODE=0
wait -n "$SEARXNG_PID" "$INFERPAGE_PID" || EXIT_CODE=$?
shutdown
exit "$EXIT_CODE"
