-- RQ524 repository-local fixture bootstrap (writable).
-- Creates the minimum auxiliary objects required by scripts/check_supplier_reconciliation_pack.sql
-- after the shared operations seed is loaded on a Trendplus-compatible PostgreSQL schema.

CREATE TABLE IF NOT EXISTS "__StartupSqlScriptHistory" (
    "ScriptPath" character varying(512) PRIMARY KEY,
    "ScriptHash" character varying(64) NOT NULL,
    "AppliedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW(),
    "DurationMs" bigint NULL
);

CREATE MATERIALIZED VIEW IF NOT EXISTS mv_supplier_decision_score_cache AS
SELECT 1::integer AS supplier_id;

CREATE MATERIALIZED VIEW IF NOT EXISTS mv_supplier_decision_score_cache_v2 AS
SELECT 1::integer AS supplier_id;

REFRESH MATERIALIZED VIEW mv_supplier_decision_score_cache;
REFRESH MATERIALIZED VIEW mv_supplier_decision_score_cache_v2;
