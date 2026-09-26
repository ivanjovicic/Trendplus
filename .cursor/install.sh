#!/usr/bin/env bash
# Idempotent repository bootstrap for the Trendplus Cloud Agent environment.
# Runs after the repository is checked out. Installs dependencies and warms the
# backend build. Long-running services are started by start.sh / terminals.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

echo "==> Frontend dependencies (npm ci)"
if [ -f Klijent/clientapp/package-lock.json ]; then
  (cd Klijent/clientapp && npm ci)
else
  (cd Klijent/clientapp && npm install)
fi

echo "==> Backend restore + build"
dotnet build Trendplus2.Backend.slnf -c Debug

echo "==> install.sh complete"
