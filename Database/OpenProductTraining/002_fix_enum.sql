DO $$ BEGIN
    CREATE TYPE dataset_split AS ENUM ('train', 'val', 'test');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

CREATE TABLE IF NOT EXISTS product_split (
    product_id  BIGINT PRIMARY KEY REFERENCES product(id) ON DELETE CASCADE,
    split       dataset_split NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_product_split_split ON product_split (split);
