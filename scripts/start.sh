#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
DOTNET_EXE="$(command -v dotnet || true)"
if [[ -z "$DOTNET_EXE" && -x "$HOME/.dotnet/dotnet" ]]; then DOTNET_EXE="$HOME/.dotnet/dotnet"; fi
if [[ -z "$DOTNET_EXE" ]]; then echo 'Instala el SDK de .NET 8.' >&2; exit 1; fi
"$DOTNET_EXE" build Eficiencia.sln --nologo
if [[ ! -d frontend/node_modules ]]; then (cd frontend && npm ci); fi
API_PID=''
WEB_PID=''
cleanup() {
  [[ -z "$WEB_PID" ]] || kill "$WEB_PID" 2>/dev/null || true
  [[ -z "$API_PID" ]] || kill "$API_PID" 2>/dev/null || true
}
trap cleanup EXIT INT TERM
(cd backend/InvoiceManager.Api && exec "$DOTNET_EXE" bin/Debug/net8.0/InvoiceManager.Api.dll --urls http://localhost:5080) &
API_PID=$!
(cd frontend && exec node node_modules/vite/bin/vite.js --host 127.0.0.1) &
WEB_PID=$!
echo 'Aplicación: http://127.0.0.1:5173 · Swagger: http://localhost:5080/swagger'
echo 'Ctrl+C para detener ambos servidores.'
wait
