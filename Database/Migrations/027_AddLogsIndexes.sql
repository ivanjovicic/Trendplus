-- Logs indexing for /api/logs queries
-- Supports:
-- 1) time-based paging/sorting
-- 2) level + time filters
-- 3) broad text search across log fields

CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE INDEX IF NOT EXISTS idx_errorrecords_timestamp_desc
    ON "ErrorRecords" ("Timestamp" DESC);

CREATE INDEX IF NOT EXISTS idx_errorrecords_level_timestamp_desc
    ON "ErrorRecords" ("Level", "Timestamp" DESC);

CREATE INDEX IF NOT EXISTS idx_errorrecords_search_trgm
    ON "ErrorRecords"
    USING gin (
        (
            coalesce("Message", '') || ' ' ||
            coalesce("ExceptionType", '') || ' ' ||
            coalesce("StackTrace", '') || ' ' ||
            coalesce("Path", '') || ' ' ||
            coalesce("UserName", '') || ' ' ||
            coalesce("ClientApp", '') || ' ' ||
            coalesce("CorrelationId", '')
        ) gin_trgm_ops
    );
