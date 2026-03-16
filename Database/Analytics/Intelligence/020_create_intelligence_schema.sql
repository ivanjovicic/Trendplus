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

COMMENT ON SCHEMA analytics_intel IS
'Versioned analytics intelligence layer for retail demand, inventory, price and trend signals. Objects in this schema are safe to evolve via vN view naming and materialized cache wrappers.';
