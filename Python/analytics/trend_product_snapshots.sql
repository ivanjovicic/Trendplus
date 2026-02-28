CREATE TABLE trend_product_snapshots (
    id              BIGSERIAL PRIMARY KEY,
    snapshot_date   DATE NOT NULL,
    canonical_key   TEXT NOT NULL,   -- match key from scorer
    product_name    TEXT NOT NULL,
    brand           TEXT NOT NULL,
    category        TEXT,
    market          TEXT,            -- "DE", "AT", etc.
    score           DOUBLE PRECISION NOT NULL,
    rank_global     INT NOT NULL,
    social_score    DOUBLE PRECISION,
    source_count    INT NOT NULL,
    unique_sources  INT NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_trend_snapshots_key_date
    ON trend_product_snapshots (canonical_key, snapshot_date DESC);