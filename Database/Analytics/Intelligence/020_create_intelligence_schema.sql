-- ==========================================================
-- 020_create_intelligence_schema.sql
-- Analytics Intelligence Layer bootstrap.
--
-- Purpose:
-- - create a dedicated schema for versioned intelligence views
-- - enable extensions required by downstream SQL intelligence assets
-- - keep execution idempotent and safe for repeated startup runs
-- ==========================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS vector;

CREATE SCHEMA IF NOT EXISTS analytics_intel;

CREATE TABLE IF NOT EXISTS analytics_intel.cache_refresh_state (
    cache_name text PRIMARY KEY,
    sql_file_path character varying(512) NOT NULL,
    build_state text NOT NULL CHECK (build_state IN ('pending', 'ready', 'failed')),
    expected_script_hash character varying(64),
    build_generation_id uuid,
    refresh_started_at_utc timestamp with time zone,
    last_refreshed_at_utc timestamp with time zone,
    metadata_version smallint NOT NULL DEFAULT 1,
    last_refresh_attempt_at_utc timestamp with time zone,
    last_refresh_status text NOT NULL DEFAULT 'unknown',
    last_refresh_failure_code text,
    last_successful_refresh_at_utc timestamp with time zone,
    last_successful_as_of_date_utc date,
    last_successful_generation_id uuid,
    last_successful_run_id bigint,
    last_successful_refresh_source text,
    failure_code text,
    updated_at_utc timestamp with time zone NOT NULL DEFAULT clock_timestamp()
);

ALTER TABLE analytics_intel.cache_refresh_state
    ADD COLUMN IF NOT EXISTS metadata_version smallint NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS last_refresh_attempt_at_utc timestamp with time zone,
    ADD COLUMN IF NOT EXISTS last_refresh_status text NOT NULL DEFAULT 'unknown',
    ADD COLUMN IF NOT EXISTS last_refresh_failure_code text,
    ADD COLUMN IF NOT EXISTS last_successful_refresh_at_utc timestamp with time zone,
    ADD COLUMN IF NOT EXISTS last_successful_as_of_date_utc date,
    ADD COLUMN IF NOT EXISTS last_successful_generation_id uuid,
    ADD COLUMN IF NOT EXISTS last_successful_run_id bigint,
    ADD COLUMN IF NOT EXISTS last_successful_refresh_source text;

COMMENT ON TABLE analytics_intel.cache_refresh_state IS
'Per-cache startup and worker refresh state. A cache is API-readable only when ready and its expected startup SQL hash matches __StartupSqlScriptHistory. Last successful generation metadata records cache refresh lineage; source-data freshness remains unknown unless an authoritative source generation is available.';

COMMENT ON SCHEMA analytics_intel IS
'Versioned analytics intelligence layer for retail demand, inventory, price and trend signals. Objects in this schema are safe to evolve via vN view naming and materialized cache wrappers.';
