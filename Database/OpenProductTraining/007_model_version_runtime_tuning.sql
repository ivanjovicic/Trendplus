-- =============================================================
-- Open Product Training: add runtime tuning payload to model registry
-- Created: 2026-03-01
-- =============================================================

ALTER TABLE IF EXISTS model_version
    ADD COLUMN IF NOT EXISTS runtime_tuning_json JSONB;

