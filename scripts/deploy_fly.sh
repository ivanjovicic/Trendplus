#!/usr/bin/env bash
# Simple helper to set Fly secrets and deploy Trendplus to Fly.io
# Usage: ./scripts/deploy_fly.sh <app-name> "<neon-connection-string>"
# Example:
# ./scripts/deploy_fly.sh trendplus "postgresql://neondb_owner:...@.../trendplus?sslmode=require&channel_binding=require"

set -euo pipefail
APP=${1:-trendplus}
NEON_CONN=${2:-}

if [ -z "$NEON_CONN" ]; then
  echo "Usage: $0 <app-name> \"<neon-connection-string>\""
  exit 2
fi

echo "Setting Fly secrets for app: $APP"
flyctl secrets set \
  ConnectionStrings__DefaultConnection="$NEON_CONN" \
  ConnectionStrings__AnalyticsConnection="$NEON_CONN" \
  ConnectionStrings__OpenProductTrainingConnection="$NEON_CONN" \
  --app "$APP"

echo "Deploying $APP..."
flyctl deploy --app "$APP"

echo "Done. Check logs with: flyctl logs --app $APP"
