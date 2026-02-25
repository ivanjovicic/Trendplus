-- ============================================================
-- Scraper Scoring Schema (MVP + Extended)
-- Target DB: Analytics PostgreSQL database
-- ============================================================
--
-- Core tables:
--   items
--   item_sources
--   runs
--   item_run_stats
--   score_components
--
-- Extended tables:
--   item_price_history
--   item_images
--   item_market_stats
--
-- Notes:
-- - This script is idempotent (CREATE ... IF NOT EXISTS).
-- - Naming is snake_case for easier SQL usage from Python.
-- - Foreign keys use ON DELETE CASCADE where lifecycle is parent-owned.

-- ============================================================
-- 1) items (canonical product model)
-- ============================================================
CREATE TABLE IF NOT EXISTS items (
    item_id BIGSERIAL PRIMARY KEY,
    canonical_key TEXT NOT NULL UNIQUE,
    brand TEXT,
    name TEXT,
    color TEXT,
    category TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_items_brand ON items (brand);
CREATE INDEX IF NOT EXISTS ix_items_category ON items (category);
CREATE INDEX IF NOT EXISTS ix_items_created_at ON items (created_at DESC);

COMMENT ON TABLE items IS 'Canonical product models used by scraper scoring.';
COMMENT ON COLUMN items.canonical_key IS 'Stable key from normalized brand + name grouping rules.';

-- ============================================================
-- 2) item_sources (canonical item across shops/markets)
-- ============================================================
CREATE TABLE IF NOT EXISTS item_sources (
    source_id BIGSERIAL PRIMARY KEY,
    item_id BIGINT NOT NULL REFERENCES items(item_id) ON DELETE CASCADE,
    source_name TEXT NOT NULL,
    market TEXT NOT NULL,
    product_url TEXT NOT NULL,
    external_product_id TEXT,
    price NUMERIC(18,4),
    currency TEXT,
    availability BOOLEAN NOT NULL DEFAULT TRUE,
    first_seen TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_seen TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_item_sources_item_source_market_url UNIQUE (item_id, source_name, market, product_url)
);

CREATE INDEX IF NOT EXISTS ix_item_sources_item_id ON item_sources (item_id);
CREATE INDEX IF NOT EXISTS ix_item_sources_source_market ON item_sources (source_name, market);
CREATE INDEX IF NOT EXISTS ix_item_sources_last_seen ON item_sources (last_seen DESC);

COMMENT ON TABLE item_sources IS 'Where and how a canonical item appears per source and market.';

-- ============================================================
-- 3) runs (one scraping/scoring cycle)
-- ============================================================
CREATE TABLE IF NOT EXISTS runs (
    run_id BIGSERIAL PRIMARY KEY,
    started_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    finished_at TIMESTAMPTZ,
    status TEXT NOT NULL DEFAULT 'completed',
    total_items INT,
    notes TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_runs_started_at ON runs (started_at DESC);
CREATE INDEX IF NOT EXISTS ix_runs_status ON runs (status);

COMMENT ON TABLE runs IS 'Metadata for each scraper execution run.';

-- ============================================================
-- 4) item_run_stats (item state per run)
-- ============================================================
CREATE TABLE IF NOT EXISTS item_run_stats (
    stat_id BIGSERIAL PRIMARY KEY,
    run_id BIGINT NOT NULL REFERENCES runs(run_id) ON DELETE CASCADE,
    item_id BIGINT NOT NULL REFERENCES items(item_id) ON DELETE CASCADE,

    base_score NUMERIC(18,6),
    final_score NUMERIC(18,6),
    rank INT,
    appearance_count INT NOT NULL DEFAULT 0,
    source_count INT NOT NULL DEFAULT 0,
    market_count INT NOT NULL DEFAULT 0,

    momentum_raw NUMERIC(18,6),
    momentum_normalized NUMERIC(18,6),

    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_item_run_stats_run_item UNIQUE (run_id, item_id),
    CONSTRAINT ck_item_run_stats_counts_nonnegative CHECK (
        appearance_count >= 0 AND source_count >= 0 AND market_count >= 0
    ),
    CONSTRAINT ck_item_run_stats_rank_positive CHECK (rank IS NULL OR rank > 0)
);

CREATE INDEX IF NOT EXISTS ix_item_run_stats_run_id ON item_run_stats (run_id);
CREATE INDEX IF NOT EXISTS ix_item_run_stats_item_id ON item_run_stats (item_id);
CREATE INDEX IF NOT EXISTS ix_item_run_stats_run_score ON item_run_stats (run_id, final_score DESC);
CREATE INDEX IF NOT EXISTS ix_item_run_stats_run_rank ON item_run_stats (run_id, rank ASC);
CREATE INDEX IF NOT EXISTS ix_item_run_stats_created_at ON item_run_stats (created_at DESC);

COMMENT ON TABLE item_run_stats IS 'Per-run scoring and rank state for each canonical item.';

-- ============================================================
-- 5) score_components (transparent score breakdown)
-- ============================================================
CREATE TABLE IF NOT EXISTS score_components (
    component_id BIGSERIAL PRIMARY KEY,
    stat_id BIGINT NOT NULL REFERENCES item_run_stats(stat_id) ON DELETE CASCADE,
    component_name TEXT NOT NULL,
    component_value NUMERIC(18,6),
    weight NUMERIC(18,6),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_score_components_stat_component UNIQUE (stat_id, component_name)
);

CREATE INDEX IF NOT EXISTS ix_score_components_stat_id ON score_components (stat_id);
CREATE INDEX IF NOT EXISTS ix_score_components_name ON score_components (component_name);

COMMENT ON TABLE score_components IS 'Explainability table: score component contributions per item_run_stats row.';

-- ============================================================
-- 6) item_price_history (extended: price intelligence)
-- ============================================================
CREATE TABLE IF NOT EXISTS item_price_history (
    price_id BIGSERIAL PRIMARY KEY,
    item_id BIGINT NOT NULL REFERENCES items(item_id) ON DELETE CASCADE,
    run_id BIGINT REFERENCES runs(run_id) ON DELETE SET NULL,
    source_name TEXT NOT NULL,
    market TEXT NOT NULL,
    price NUMERIC(18,4) NOT NULL,
    currency TEXT NOT NULL,
    scraped_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_item_price_history_price_positive CHECK (price > 0)
);

CREATE INDEX IF NOT EXISTS ix_item_price_history_item_scraped_at
    ON item_price_history (item_id, scraped_at DESC);
CREATE INDEX IF NOT EXISTS ix_item_price_history_source_market_scraped_at
    ON item_price_history (source_name, market, scraped_at DESC);

COMMENT ON TABLE item_price_history IS 'Historical pricing snapshots by source and market.';

-- ============================================================
-- 7) item_images (extended: CV-ready metadata)
-- ============================================================
CREATE TABLE IF NOT EXISTS item_images (
    image_id BIGSERIAL PRIMARY KEY,
    item_id BIGINT NOT NULL REFERENCES items(item_id) ON DELETE CASCADE,
    image_url TEXT NOT NULL,
    dominant_color TEXT,
    heel_height NUMERIC(8,3),
    toe_shape TEXT,
    outsole_thickness NUMERIC(8,3),
    cv_embedding BYTEA,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_item_images_item_url UNIQUE (item_id, image_url)
);

CREATE INDEX IF NOT EXISTS ix_item_images_item_id ON item_images (item_id);
CREATE INDEX IF NOT EXISTS ix_item_images_created_at ON item_images (created_at DESC);

COMMENT ON TABLE item_images IS 'Image metadata and optional embedding blob for vision-based features.';

-- ============================================================
-- 8) item_market_stats (extended: per-market ranking state)
-- ============================================================
CREATE TABLE IF NOT EXISTS item_market_stats (
    id BIGSERIAL PRIMARY KEY,
    item_id BIGINT NOT NULL REFERENCES items(item_id) ON DELETE CASCADE,
    run_id BIGINT NOT NULL REFERENCES runs(run_id) ON DELETE CASCADE,
    market TEXT NOT NULL,
    rank INT,
    score NUMERIC(18,6),
    appearance_count INT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_item_market_stats_item_run_market UNIQUE (item_id, run_id, market),
    CONSTRAINT ck_item_market_stats_appearance_nonnegative CHECK (appearance_count >= 0),
    CONSTRAINT ck_item_market_stats_rank_positive CHECK (rank IS NULL OR rank > 0)
);

CREATE INDEX IF NOT EXISTS ix_item_market_stats_run_market_score
    ON item_market_stats (run_id, market, score DESC);
CREATE INDEX IF NOT EXISTS ix_item_market_stats_item_id ON item_market_stats (item_id);

COMMENT ON TABLE item_market_stats IS 'Per-run market-level ranking and score slices.';

-- ============================================================
-- 9) Helper view: latest run leaderboard
-- ============================================================
CREATE OR REPLACE VIEW vw_latest_item_scores AS
SELECT
    irs.stat_id,
    irs.run_id,
    i.item_id,
    i.canonical_key,
    i.brand,
    i.name,
    i.category,
    irs.base_score,
    irs.final_score,
    irs.rank,
    irs.appearance_count,
    irs.source_count,
    irs.market_count,
    irs.momentum_raw,
    irs.momentum_normalized,
    irs.created_at
FROM item_run_stats irs
JOIN items i ON i.item_id = irs.item_id
WHERE irs.run_id = (SELECT MAX(run_id) FROM runs)
ORDER BY irs.final_score DESC NULLS LAST, irs.rank ASC NULLS LAST;

COMMENT ON VIEW vw_latest_item_scores IS 'Top scored items from the latest run.';

-- ============================================================
-- 10) Quick verification
-- ============================================================
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name IN (
      'items',
      'item_sources',
      'runs',
      'item_run_stats',
      'score_components',
      'item_price_history',
      'item_images',
      'item_market_stats'
  )
ORDER BY table_name;
