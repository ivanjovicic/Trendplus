-- Creates a simple archive table to store deleted rows as JSON for possible rollback
CREATE TABLE IF NOT EXISTS deleted_rows_archive (
    id BIGSERIAL PRIMARY KEY,
    batch_id BIGINT NULL,
    table_name TEXT NOT NULL,
    primary_key JSONB NULL,
    row_json JSONB NOT NULL,
    deleted_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_by TEXT NULL,
    reason TEXT NULL
);

-- Optional index for quick lookups by table and deleted_at
CREATE INDEX IF NOT EXISTS idx_deleted_rows_archive_table_deleted_at ON deleted_rows_archive(table_name, deleted_at DESC);
