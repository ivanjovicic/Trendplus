#!/usr/bin/env bash
# Per-boot startup for the Trendplus Cloud Agent environment.
# Brings up the local PostgreSQL 16 cluster on port 5434 (the port the committed
# appsettings.Development.json expects) and ensures the primary database + pgvector
# extension exist. Idempotent and safe to run on every boot.
#
# The backend API and frontend dev server are launched as persistent `terminals`
# (see .cursor/environment.json), not here.
set -euo pipefail

PG_PORT=5434
PG_VERSION=16
PG_CLUSTER=main
PG_CONF="/etc/postgresql/${PG_VERSION}/${PG_CLUSTER}/postgresql.conf"

echo "==> Configuring PostgreSQL cluster ${PG_VERSION}/${PG_CLUSTER} on port ${PG_PORT}"
if [ -f "$PG_CONF" ]; then
  sudo sed -i -E "s/^[# ]*port\s*=\s*[0-9]+/port = ${PG_PORT}/" "$PG_CONF"
fi

# Start (or restart) the cluster. pg_ctlcluster is idempotent enough that we
# tolerate an already-running cluster.
if ! sudo pg_ctlcluster "$PG_VERSION" "$PG_CLUSTER" start 2>/dev/null; then
  sudo pg_ctlcluster "$PG_VERSION" "$PG_CLUSTER" restart || true
fi

echo "==> Waiting for PostgreSQL to accept connections on ${PG_PORT}"
for _ in $(seq 1 30); do
  if sudo -u postgres pg_isready -p "$PG_PORT" >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

echo "==> Ensuring role password + primary database + pgvector extension"
sudo -u postgres psql -p "$PG_PORT" -v ON_ERROR_STOP=1 -c "ALTER USER postgres WITH PASSWORD 'postgres';"
if ! sudo -u postgres psql -p "$PG_PORT" -tAc "SELECT 1 FROM pg_database WHERE datname='trendplus'" | grep -q 1; then
  sudo -u postgres psql -p "$PG_PORT" -c "CREATE DATABASE trendplus;"
fi
sudo -u postgres psql -p "$PG_PORT" -d trendplus -c "CREATE EXTENSION IF NOT EXISTS vector;"

echo "==> PostgreSQL ready on 127.0.0.1:${PG_PORT} (database: trendplus)"
echo "==> start.sh complete"
