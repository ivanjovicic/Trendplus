-- ============================================================
-- Migration 015: Enhanced Access Import — log table, cleanup proc, indexes
-- Safe to run multiple times (IF NOT EXISTS everywhere).
-- ============================================================

-- ── 1) Enhanced batch history: add duration + row counters ──

ALTER TABLE "DataImportBatches"
    ADD COLUMN IF NOT EXISTS "DurationSeconds" INTEGER NULL;

ALTER TABLE "DataImportBatches"
    ADD COLUMN IF NOT EXISTS "TotalImported" INTEGER NOT NULL DEFAULT 0;

ALTER TABLE "DataImportBatches"
    ADD COLUMN IF NOT EXISTS "TotalUpdated" INTEGER NOT NULL DEFAULT 0;

ALTER TABLE "DataImportBatches"
    ADD COLUMN IF NOT EXISTS "TotalErrors" INTEGER NOT NULL DEFAULT 0;

ALTER TABLE "DataImportBatches"
    ADD COLUMN IF NOT EXISTS "DataOrigin" VARCHAR(32) NOT NULL DEFAULT 'access';

-- ── 2) Per-row import log (errors, warnings, row-level tracking) ──

CREATE TABLE IF NOT EXISTS "AccessImportLog" (
    "Id"           BIGSERIAL PRIMARY KEY,
    "BatchId"      BIGINT NOT NULL REFERENCES "DataImportBatches"("Id") ON DELETE CASCADE,
    "TableName"    VARCHAR(128) NOT NULL,
    "RowIndex"     INTEGER NOT NULL DEFAULT 0,
    "Severity"     VARCHAR(16) NOT NULL DEFAULT 'info',   -- info / warning / error
    "Message"      VARCHAR(2000) NOT NULL DEFAULT '',
    "SourceRowJson" TEXT NULL,
    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_AccessImportLog_BatchId"
    ON "AccessImportLog" ("BatchId");

CREATE INDEX IF NOT EXISTS "IX_AccessImportLog_Severity"
    ON "AccessImportLog" ("Severity");

CREATE INDEX IF NOT EXISTS "IX_AccessImportLog_BatchId_TableName"
    ON "AccessImportLog" ("BatchId", "TableName");

-- ── 3) Add BatchId FK to entities for cascade tracking ──

ALTER TABLE "Artikli"
    ADD COLUMN IF NOT EXISTS "BatchId" BIGINT NULL;

ALTER TABLE "Dobavljaci"
    ADD COLUMN IF NOT EXISTS "BatchId" BIGINT NULL;

ALTER TABLE "Sezone"
    ADD COLUMN IF NOT EXISTS "BatchId" BIGINT NULL;

ALTER TABLE "TipoviObuce"
    ADD COLUMN IF NOT EXISTS "BatchId" BIGINT NULL;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'prodaja_zaglavlje') THEN
        EXECUTE 'ALTER TABLE prodaja_zaglavlje ADD COLUMN IF NOT EXISTS batch_id BIGINT NULL';
    END IF;
END $$;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'prodaja_stavke') THEN
        EXECUTE 'ALTER TABLE prodaja_stavke ADD COLUMN IF NOT EXISTS batch_id BIGINT NULL';
    END IF;
END $$;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'dnevnik_promena') THEN
        EXECUTE 'ALTER TABLE dnevnik_promena ADD COLUMN IF NOT EXISTS batch_id BIGINT NULL';
    END IF;
END $$;

-- BatchId indexes for fast cascade deletes
CREATE INDEX IF NOT EXISTS "IX_Artikli_BatchId" ON "Artikli" ("BatchId") WHERE "BatchId" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_Dobavljaci_BatchId" ON "Dobavljaci" ("BatchId") WHERE "BatchId" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_Sezone_BatchId" ON "Sezone" ("BatchId") WHERE "BatchId" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_TipoviObuce_BatchId" ON "TipoviObuce" ("BatchId") WHERE "BatchId" IS NOT NULL;

-- ── 4) Stored procedure: delete_batch (cascading cleanup) ──

CREATE OR REPLACE FUNCTION delete_batch(p_batch_id BIGINT, p_include_analytics BOOLEAN DEFAULT TRUE)
RETURNS TABLE (
    batch_found       BOOLEAN,
    artikli_deleted    INTEGER,
    dobavljaci_deleted INTEGER,
    sezone_deleted     INTEGER,
    tipovi_deleted     INTEGER,
    prodaja_deleted    INTEGER,
    stavke_deleted     INTEGER,
    dnevnik_deleted    INTEGER,
    log_deleted        INTEGER,
    analytics_deleted  INTEGER
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_batch_found       BOOLEAN := FALSE;
    v_artikli_deleted    INTEGER := 0;
    v_dobavljaci_deleted INTEGER := 0;
    v_sezone_deleted     INTEGER := 0;
    v_tipovi_deleted     INTEGER := 0;
    v_prodaja_deleted    INTEGER := 0;
    v_stavke_deleted     INTEGER := 0;
    v_dnevnik_deleted    INTEGER := 0;
    v_log_deleted        INTEGER := 0;
    v_analytics_deleted  INTEGER := 0;
BEGIN
    -- Check batch exists
    SELECT EXISTS(SELECT 1 FROM "DataImportBatches" WHERE "Id" = p_batch_id) INTO v_batch_found;
    IF NOT v_batch_found THEN
        RETURN QUERY SELECT FALSE, 0, 0, 0, 0, 0, 0, 0, 0, 0;
        RETURN;
    END IF;

    -- Delete import log entries (cascade handles this, but be explicit)
    DELETE FROM "AccessImportLog" WHERE "BatchId" = p_batch_id;
    GET DIAGNOSTICS v_log_deleted = ROW_COUNT;

    -- Delete Trendplus entities by DataOrigin + BatchId
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'prodaja_stavke') THEN
        EXECUTE format('DELETE FROM prodaja_stavke WHERE batch_id = %L', p_batch_id);
        GET DIAGNOSTICS v_stavke_deleted = ROW_COUNT;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'prodaja_zaglavlje') THEN
        EXECUTE format('DELETE FROM prodaja_zaglavlje WHERE batch_id = %L', p_batch_id);
        GET DIAGNOSTICS v_prodaja_deleted = ROW_COUNT;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'dnevnik_promena') THEN
        EXECUTE format('DELETE FROM dnevnik_promena WHERE batch_id = %L', p_batch_id);
        GET DIAGNOSTICS v_dnevnik_deleted = ROW_COUNT;
    END IF;

    DELETE FROM "Artikli" WHERE "BatchId" = p_batch_id;
    GET DIAGNOSTICS v_artikli_deleted = ROW_COUNT;

    DELETE FROM "Dobavljaci" WHERE "BatchId" = p_batch_id;
    GET DIAGNOSTICS v_dobavljaci_deleted = ROW_COUNT;

    DELETE FROM "Sezone" WHERE "BatchId" = p_batch_id;
    GET DIAGNOSTICS v_sezone_deleted = ROW_COUNT;

    DELETE FROM "TipoviObuce" WHERE "BatchId" = p_batch_id;
    GET DIAGNOSTICS v_tipovi_deleted = ROW_COUNT;

    -- Analytics cleanup
    IF p_include_analytics THEN
        -- Generic analytics cleanup: delete all analytics rows linked to this batch
        -- The actual analytics tables depend on schema, using convention:
        -- products_dim, sales_facts, sales_line_facts have data_origin + batch_id columns
        BEGIN
            EXECUTE format('DELETE FROM products_dim WHERE data_origin = ''access'' AND batch_id = %L', p_batch_id);
            GET DIAGNOSTICS v_analytics_deleted = ROW_COUNT;
        EXCEPTION WHEN undefined_table OR undefined_column THEN
            NULL;
        END;

        BEGIN
            EXECUTE format('DELETE FROM sales_facts WHERE data_origin = ''access'' AND batch_id = %L', p_batch_id);
            v_analytics_deleted := v_analytics_deleted + (SELECT COUNT(*) FROM (SELECT 1) x WHERE FALSE);
        EXCEPTION WHEN undefined_table OR undefined_column THEN
            NULL;
        END;

        BEGIN
            EXECUTE format('DELETE FROM sales_line_facts WHERE data_origin = ''access'' AND batch_id = %L', p_batch_id);
        EXCEPTION WHEN undefined_table OR undefined_column THEN
            NULL;
        END;
    END IF;

    -- Finally delete the batch record itself
    DELETE FROM "DataImportBatches" WHERE "Id" = p_batch_id;

    RETURN QUERY SELECT v_batch_found, v_artikli_deleted, v_dobavljaci_deleted,
        v_sezone_deleted, v_tipovi_deleted, v_prodaja_deleted, v_stavke_deleted,
        v_dnevnik_deleted, v_log_deleted, v_analytics_deleted;
END;
$$;

-- ── 5) Trigger: auto-set DurationSeconds on batch completion ──

CREATE OR REPLACE FUNCTION trg_batch_set_duration()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW."CompletedAtUtc" IS NOT NULL AND OLD."CompletedAtUtc" IS NULL THEN
        NEW."DurationSeconds" := EXTRACT(EPOCH FROM (NEW."CompletedAtUtc" - NEW."StartedAtUtc"))::INTEGER;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_batch_duration ON "DataImportBatches";
CREATE TRIGGER trg_batch_duration
    BEFORE UPDATE ON "DataImportBatches"
    FOR EACH ROW
    EXECUTE FUNCTION trg_batch_set_duration();
