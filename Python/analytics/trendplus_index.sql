CREATE TABLE trendplus_index (
    id                   BIGSERIAL PRIMARY KEY,
    snapshot_date        DATE NOT NULL,
    scope_type           TEXT NOT NULL,   -- "market" | "brand" | "category" | "brand_market"
    scope_value          TEXT NOT NULL,   -- e.g. "DE", "nike", "sneaker", "nike|de"
    index_value          DOUBLE PRECISION NOT NULL,
    base_component       DOUBLE PRECISION NOT NULL,
    momentum_component   DOUBLE PRECISION NOT NULL,
    social_component     DOUBLE PRECISION NOT NULL,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX idx_trendplus_index_scope_date
    ON trendplus_index (scope_type, scope_value, snapshot_date DESC);

CREATE INDEX idx_trendplus_index_date
    ON trendplus_index (snapshot_date DESC);