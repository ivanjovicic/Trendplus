-- =============================================================
-- Open Product Training 2.0 schema extensions
-- PostgreSQL 16 + pgvector 0.6+
-- Created: 2026-02-28
-- Idempotent: safe to re-run
-- =============================================================

-- pgvector extension (required for vector(...) columns + HNSW indexes)
CREATE EXTENSION IF NOT EXISTS vector;

-- -------------------------------------------------------------
-- 1) Training runs (experiment tracking)
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS training_run (
    id                BIGSERIAL PRIMARY KEY,
    model_type        TEXT        NOT NULL,                 -- e.g. 'sell_probability_rs'
    dataset_id        INT         NULL REFERENCES dataset(id) ON DELETE SET NULL,
    feature_view_name TEXT        NOT NULL DEFAULT 'vw_feature_store',
    status            TEXT        NOT NULL DEFAULT 'queued', -- queued|running|succeeded|failed|canceled
    started_at        TIMESTAMPTZ NULL,
    completed_at      TIMESTAMPTZ NULL,
    code_version      TEXT        NULL,                     -- git sha / build version
    params_json       JSONB       NULL,
    metrics_json      JSONB       NULL,
    artifact_uri      TEXT        NULL,                     -- path or s3 uri (optional)
    notes             TEXT        NULL,
    error_message     TEXT        NULL,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_training_run_model_created_at
    ON training_run (model_type, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_training_run_status
    ON training_run (status);

-- -------------------------------------------------------------
-- 2) Model registry (versioned, active model per model_type)
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS model_version (
    id                      BIGSERIAL PRIMARY KEY,
    model_type              TEXT        NOT NULL,
    version                 INT         NOT NULL,
    training_run_id         BIGINT      NULL REFERENCES training_run(id) ON DELETE SET NULL,
    is_active               BOOLEAN     NOT NULL DEFAULT FALSE,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- ONNX artifact
    onnx_path               TEXT        NULL,              -- file path within container / volume
    onnx_sha256             TEXT        NULL,

    -- Contract / metadata
    feature_schema_json     JSONB       NULL,              -- ordered list of features + dtypes
    metrics_json            JSONB       NULL,              -- auc/brier/f1/rmse...
    calibration_json        JSONB       NULL,              -- platt params, bins, etc
    shap_summary_json       JSONB       NULL,              -- global shap summary
    feature_importance_json JSONB       NULL,              -- global feature importance
    runtime_tuning_json     JSONB       NULL,              -- runtime scoring heuristic tuning snapshot
    min_feature_values      JSONB       NULL,
    max_feature_values      JSONB       NULL,
    notes                   TEXT        NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_model_version_type_version
    ON model_version (model_type, version);

-- Enforce one active model per model_type
CREATE UNIQUE INDEX IF NOT EXISTS ux_model_version_active_per_type
    ON model_version (model_type)
    WHERE is_active;

CREATE INDEX IF NOT EXISTS idx_model_version_created_at
    ON model_version (created_at DESC);

-- -------------------------------------------------------------
-- 3) Brand/category normalization (canonical mapping)
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS brand_normalized (
    id             BIGSERIAL PRIMARY KEY,
    raw_brand      TEXT        NOT NULL,
    normalized_key TEXT        NOT NULL,
    brand_id       INT         NULL REFERENCES brand(id) ON DELETE SET NULL,
    confidence     NUMERIC(6,4) NULL,
    source         TEXT        NULL,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (raw_brand)
);

CREATE INDEX IF NOT EXISTS idx_brand_normalized_key
    ON brand_normalized (normalized_key);

CREATE TABLE IF NOT EXISTS category_normalized (
    id             BIGSERIAL PRIMARY KEY,
    raw_category   TEXT        NOT NULL,
    normalized_key TEXT        NOT NULL,
    category_id    INT         NULL REFERENCES category(id) ON DELETE SET NULL,
    confidence     NUMERIC(6,4) NULL,
    source         TEXT        NULL,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (raw_category)
);

CREATE INDEX IF NOT EXISTS idx_category_normalized_key
    ON category_normalized (normalized_key);

-- -------------------------------------------------------------
-- 4) Product quality flags (data QA + training exclusions)
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS product_quality_flags (
    id              BIGSERIAL PRIMARY KEY,
    product_id      BIGINT      NOT NULL REFERENCES product(id) ON DELETE CASCADE,
    flag_key        TEXT        NOT NULL,                 -- e.g. 'missing_price', 'outlier_price'
    severity        SMALLINT    NOT NULL DEFAULT 1,       -- 1=info 2=warn 3=error
    details         JSONB       NULL,
    training_run_id BIGINT      NULL REFERENCES training_run(id) ON DELETE SET NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (product_id, flag_key)
);

CREATE INDEX IF NOT EXISTS idx_product_quality_flags_product
    ON product_quality_flags (product_id);

CREATE INDEX IF NOT EXISTS idx_product_quality_flags_severity
    ON product_quality_flags (severity);

-- -------------------------------------------------------------
-- 5) RS sell probability label (supervised target for RS market)
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS training_label_sell_probability_rs (
    id            BIGSERIAL PRIMARY KEY,
    product_id    BIGINT      NOT NULL REFERENCES product(id) ON DELETE CASCADE,
    horizon_days  INT         NOT NULL DEFAULT 30,
    label_value   NUMERIC(10,6) NOT NULL,                 -- [0..1]
    label_version TEXT        NOT NULL DEFAULT 'v1',
    as_of_date    DATE        NULL,
    computed_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    source        TEXT        NULL,
    notes         TEXT        NULL,
    CONSTRAINT ck_sell_probability_01 CHECK (label_value >= 0 AND label_value <= 1),
    UNIQUE (product_id, horizon_days, label_version)
);

CREATE INDEX IF NOT EXISTS idx_sellprob_product
    ON training_label_sell_probability_rs (product_id);

CREATE INDEX IF NOT EXISTS idx_sellprob_horizon
    ON training_label_sell_probability_rs (horizon_days);

-- -------------------------------------------------------------
-- 6) Feature vectors (pgvector-native)
--    - text embedding (256D + optional PCA 64D)
--    - image embedding v2 (256D + optional PCA 64D + cluster_id)
--    - price-history engineered feature snapshot
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS product_feature_vector_text (
    id              BIGSERIAL PRIMARY KEY,
    product_id      BIGINT      NOT NULL REFERENCES product(id) ON DELETE CASCADE,
    embedding_model TEXT        NOT NULL,                 -- e.g. 'e5-small-v2'
    embedding       vector(256) NOT NULL,
    embedding_pca_64 vector(64) NULL,
    text_hash       TEXT        NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (product_id, embedding_model)
);

CREATE INDEX IF NOT EXISTS idx_pfvt_product
    ON product_feature_vector_text (product_id);

CREATE INDEX IF NOT EXISTS idx_pfvt_created_at
    ON product_feature_vector_text (created_at DESC);

CREATE INDEX IF NOT EXISTS idx_pfvt_embedding_hnsw
    ON product_feature_vector_text
    USING hnsw (embedding vector_cosine_ops)
    WITH (m = 16, ef_construction = 64);

CREATE TABLE IF NOT EXISTS product_feature_vector_image_v2 (
    id               BIGSERIAL PRIMARY KEY,
    product_id       BIGINT      NOT NULL REFERENCES product(id) ON DELETE CASCADE,
    embedding_model  TEXT        NOT NULL,                -- e.g. 'resnet50-avgpool'
    embedding_256    vector(256) NOT NULL,
    embedding_pca_64 vector(64)  NULL,
    cluster_id       INT         NULL,
    cluster_distance NUMERIC(12,6) NULL,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (product_id, embedding_model)
);

CREATE INDEX IF NOT EXISTS idx_pfvi2_product
    ON product_feature_vector_image_v2 (product_id);

CREATE INDEX IF NOT EXISTS idx_pfvi2_cluster
    ON product_feature_vector_image_v2 (cluster_id);

CREATE INDEX IF NOT EXISTS idx_pfvi2_embedding_hnsw
    ON product_feature_vector_image_v2
    USING hnsw (embedding_256 vector_cosine_ops)
    WITH (m = 16, ef_construction = 64);

CREATE TABLE IF NOT EXISTS product_feature_vector_price_history (
    id                    BIGSERIAL PRIMARY KEY,
    product_id            BIGINT      NOT NULL REFERENCES product(id) ON DELETE CASCADE,
    feature_version       TEXT        NOT NULL DEFAULT 'v1',
    computed_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    currency              TEXT        NULL,
    price_obs_count       INT         NULL,

    volatility_7d         NUMERIC(18,6) NULL,
    volatility_30d        NUMERIC(18,6) NULL,
    volatility_90d        NUMERIC(18,6) NULL,
    momentum_7d           NUMERIC(18,6) NULL,
    momentum_30d          NUMERIC(18,6) NULL,
    momentum_90d          NUMERIC(18,6) NULL,
    discount_freq_30d     NUMERIC(18,6) NULL,
    discount_freq_90d     NUMERIC(18,6) NULL,
    typical_change_rate_30d NUMERIC(18,6) NULL,

    vector                vector(32) NULL,               -- optional compact representation
    details               JSONB       NULL,

    UNIQUE (product_id, feature_version)
);

CREATE INDEX IF NOT EXISTS idx_pfvph_product
    ON product_feature_vector_price_history (product_id);

CREATE INDEX IF NOT EXISTS idx_pfvph_computed_at
    ON product_feature_vector_price_history (computed_at DESC);
