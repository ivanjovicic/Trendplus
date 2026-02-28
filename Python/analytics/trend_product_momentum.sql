CREATE TABLE trend_product_momentum (
    id              BIGSERIAL PRIMARY KEY,
    snapshot_date   DATE NOT NULL,
    canonical_key   TEXT NOT NULL,
    momentum_score  DOUBLE PRECISION NOT NULL,
    score_delta     DOUBLE PRECISION NOT NULL,
    rank_delta      INT NOT NULL,
    is_new_entry    BOOLEAN NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);