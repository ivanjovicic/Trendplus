CREATE TABLE inventory_recommendations (
    id               BIGSERIAL PRIMARY KEY,
    snapshot_date    DATE NOT NULL,
    product_id       TEXT NOT NULL,
    brand            TEXT,
    category         TEXT,
    sales_velocity   DOUBLE PRECISION NOT NULL DEFAULT 0,
    stock_on_hand    DOUBLE PRECISION NOT NULL DEFAULT 0,
    trend_score      DOUBLE PRECISION NOT NULL DEFAULT 0,
    momentum_score   DOUBLE PRECISION NOT NULL DEFAULT 0,
    recommended_qty  INT NOT NULL,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_inv_rec_date ON inventory_recommendations (snapshot_date DESC);
CREATE INDEX idx_inv_rec_product ON inventory_recommendations (product_id, snapshot_date DESC);