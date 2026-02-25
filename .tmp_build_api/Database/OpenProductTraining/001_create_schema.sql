-- =============================================================
-- open_product_training schema
-- PostgreSQL | za treniranje popularity/embedding modela
-- Created: 2026-02-22
-- =============================================================

-- -----------------------------------------------
-- 1. Dataset meta
-- -----------------------------------------------
CREATE TABLE IF NOT EXISTS dataset (
    id            SERIAL PRIMARY KEY,
    name          VARCHAR(100) NOT NULL,          -- 'amazon_clothing_shoes', 'ut_zappos50k'
    source_type   VARCHAR(50)  NOT NULL,          -- 'amazon', 'zappos', 'kaggle', 'custom'
    description   TEXT,
    license       TEXT,
    raw_location  TEXT,                           -- .csv/.json putanja (disk ili S3)
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- -----------------------------------------------
-- 2. Sirovi dump (originalni payload)
-- -----------------------------------------------
CREATE TABLE IF NOT EXISTS raw_product (
    id            BIGSERIAL PRIMARY KEY,
    dataset_id    INT  NOT NULL REFERENCES dataset(id) ON DELETE CASCADE,
    external_id   VARCHAR(255) NOT NULL,          -- ASIN, SKU, itd.
    raw_payload   JSONB NOT NULL,
    imported_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (dataset_id, external_id)
);

-- -----------------------------------------------
-- 3. Pomoćne lookup tabele
-- -----------------------------------------------
CREATE TABLE IF NOT EXISTS brand (
    id    SERIAL PRIMARY KEY,
    name  VARCHAR(100) UNIQUE NOT NULL
);

CREATE TABLE IF NOT EXISTS category (
    id        SERIAL PRIMARY KEY,
    name      VARCHAR(100) UNIQUE NOT NULL,       -- "Shoes", "Sneakers", …
    parent_id INT REFERENCES category(id)
);

-- -----------------------------------------------
-- 4. Normalizovani proizvodi (core tabela)
-- -----------------------------------------------
CREATE TABLE IF NOT EXISTS product (
    id              BIGSERIAL PRIMARY KEY,
    dataset_id      INT  NOT NULL REFERENCES dataset(id)   ON DELETE CASCADE,
    external_id     VARCHAR(255) NOT NULL,

    brand_id        INT  REFERENCES brand(id),
    category_id     INT  REFERENCES category(id),

    title           TEXT NOT NULL,
    description     TEXT,

    gender          VARCHAR(20),                  -- 'men', 'women', 'unisex', 'kids'
    shoe_type       VARCHAR(50),                  -- 'sneakers', 'boots', 'heels', …

    currency        VARCHAR(10),
    price           NUMERIC(10,2),

    avg_rating      NUMERIC(3,2),
    review_count    INT,

    main_image_url  TEXT,

    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    UNIQUE (dataset_id, external_id)
);

-- -----------------------------------------------
-- 5. Slike po proizvodu
-- -----------------------------------------------
CREATE TABLE IF NOT EXISTS product_image (
    id          BIGSERIAL PRIMARY KEY,
    product_id  BIGINT NOT NULL REFERENCES product(id) ON DELETE CASCADE,
    image_url   TEXT,
    local_path  TEXT,                             -- lokalno / S3
    is_primary  BOOLEAN NOT NULL DEFAULT FALSE
);

-- -----------------------------------------------
-- 6. Atributi (fleksibilan K/V)
-- -----------------------------------------------
CREATE TABLE IF NOT EXISTS product_attribute (
    id                BIGSERIAL PRIMARY KEY,
    product_id        BIGINT NOT NULL REFERENCES product(id) ON DELETE CASCADE,
    key               VARCHAR(100) NOT NULL,      -- 'material', 'heel_height', 'color', …
    value_raw         TEXT,
    value_normalized  TEXT,
    UNIQUE (product_id, key)
);

-- -----------------------------------------------
-- 7. Price history
-- -----------------------------------------------
CREATE TABLE IF NOT EXISTS product_price_history (
    id           BIGSERIAL PRIMARY KEY,
    product_id   BIGINT NOT NULL REFERENCES product(id) ON DELETE CASCADE,
    currency     VARCHAR(10) NOT NULL,
    price        NUMERIC(10,2) NOT NULL,
    collected_at TIMESTAMPTZ NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_price_history_product_time
    ON product_price_history (product_id, collected_at);

-- -----------------------------------------------
-- 8. Review agregati
-- -----------------------------------------------
CREATE TABLE IF NOT EXISTS product_review_stats (
    product_id        BIGINT PRIMARY KEY REFERENCES product(id) ON DELETE CASCADE,
    avg_rating        NUMERIC(3,2),
    rating_count      INT,
    review_text_count INT
);

-- -----------------------------------------------
-- 9. Training labels (ground truth)
-- -----------------------------------------------
CREATE TABLE IF NOT EXISTS training_label (
    id             BIGSERIAL PRIMARY KEY,
    product_id     BIGINT NOT NULL REFERENCES product(id) ON DELETE CASCADE,
    label_type     VARCHAR(50) NOT NULL,          -- 'popularity_score', 'is_top_10_percent', 'log_review_count', …
    value_numeric  NUMERIC(12,4),
    value_text     TEXT,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_training_label_product
    ON training_label (product_id);

CREATE INDEX IF NOT EXISTS idx_training_label_type
    ON training_label (label_type);

-- -----------------------------------------------
-- 10. Train / val / test split
-- -----------------------------------------------
DO $$ BEGIN
    CREATE TYPE dataset_split AS ENUM ('train', 'val', 'test');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

CREATE TABLE IF NOT EXISTS product_split (
    product_id  BIGINT PRIMARY KEY REFERENCES product(id) ON DELETE CASCADE,
    split       dataset_split NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_product_split_split
    ON product_split (split);

-- -----------------------------------------------
-- 11. Feature / embedding vektori
-- -----------------------------------------------
CREATE TABLE IF NOT EXISTS product_feature_vector (
    id            BIGSERIAL PRIMARY KEY,
    product_id    BIGINT NOT NULL REFERENCES product(id) ON DELETE CASCADE,
    feature_type  VARCHAR(50) NOT NULL,           -- 'image_embedding', 'text_embedding', 'tabular'
    vector_dim    INT NOT NULL,
    vector        BYTEA NOT NULL,                 -- binary float32[] | zameni sa VECTOR(dim) ako koristiš pgvector
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (product_id, feature_type)
);

-- -----------------------------------------------
-- 12. Quality-of-life indeksi
-- -----------------------------------------------
CREATE INDEX IF NOT EXISTS idx_product_dataset  ON product (dataset_id);
CREATE INDEX IF NOT EXISTS idx_product_brand    ON product (brand_id);
CREATE INDEX IF NOT EXISTS idx_product_cat      ON product (category_id);
CREATE INDEX IF NOT EXISTS idx_product_price    ON product (price);
CREATE INDEX IF NOT EXISTS idx_product_rating   ON product (avg_rating);
CREATE INDEX IF NOT EXISTS idx_product_gender   ON product (gender);
CREATE INDEX IF NOT EXISTS idx_product_type     ON product (shoe_type);

CREATE INDEX IF NOT EXISTS idx_raw_product_dataset ON raw_product (dataset_id);

-- -----------------------------------------------
-- Primer seed podataka – dataset zapisi
-- -----------------------------------------------
-- INSERT INTO dataset (name, source_type, description, license)
-- VALUES
--     ('amazon_clothing_shoes', 'amazon',  'Amazon product metadata – clothing/shoes subset', 'Amazon Research'),
--     ('ut_zappos50k',          'zappos',  'UT Zappos50K fine-grained shoe dataset',           'UT Austin'),
--     ('kaggle_fashion_shoes',  'kaggle',  'Kaggle fashion shoes dataset',                     'Kaggle CC');
