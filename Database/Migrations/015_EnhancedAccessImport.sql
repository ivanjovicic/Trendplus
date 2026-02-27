-- ============================================================
-- Migration 015 (REFactored & Optimized)
-- PostgreSQL 15
-- Fully idempotent
-- ============================================================

-- 1️⃣ Enhance DataImportBatches safely
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'DataImportBatches'
          AND column_name = 'DurationSeconds'
    ) THEN
        ALTER TABLE "DataImportBatches"
            ADD COLUMN "DurationSeconds" INTEGER;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'DataImportBatches'
          AND column_name = 'TotalImported'
    ) THEN
        ALTER TABLE "DataImportBatches"
            ADD COLUMN "TotalImported" INTEGER NOT NULL DEFAULT 0,
            ADD COLUMN "TotalUpdated" INTEGER NOT NULL DEFAULT 0,
            ADD COLUMN "TotalErrors" INTEGER NOT NULL DEFAULT 0,
            ADD COLUMN "DataOrigin" VARCHAR(32) NOT NULL DEFAULT 'access';
    END IF;
END $$;

-- 2️⃣ AccessImportLog (sa jačim indeksima)
CREATE TABLE IF NOT EXISTS "AccessImportLog" (
    "Id" BIGSERIAL PRIMARY KEY,
    "BatchId" BIGINT NOT NULL,
    "TableName" VARCHAR(128) NOT NULL,
    "RowIndex" INTEGER NOT NULL DEFAULT 0,
    "Severity" VARCHAR(16) NOT NULL DEFAULT 'info',
    "Message" VARCHAR(2000) NOT NULL DEFAULT '',
    "SourceRowJson" JSONB NULL,
    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_accesslog_batch
        FOREIGN KEY ("BatchId")
        REFERENCES "DataImportBatches"("Id")
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_accesslog_batch
    ON "AccessImportLog"("BatchId");

CREATE INDEX IF NOT EXISTS idx_accesslog_batch_severity
    ON "AccessImportLog"("BatchId","Severity");

-- 3️⃣ BatchId FK + indeks (bez dynamic EXECUTE)
ALTER TABLE "Artikli"
    ADD COLUMN IF NOT EXISTS "BatchId" BIGINT;
ALTER TABLE "Artikli"
    ADD CONSTRAINT IF NOT EXISTS fk_artikli_batch
    FOREIGN KEY ("BatchId")
    REFERENCES "DataImportBatches"("Id")
    ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS idx_artikli_batch
    ON "Artikli"("BatchId")
    WHERE "BatchId" IS NOT NULL;

-- Repeat for other tables
ALTER TABLE "Dobavljaci"
    ADD COLUMN IF NOT EXISTS "BatchId" BIGINT;
ALTER TABLE "Dobavljaci"
    ADD CONSTRAINT IF NOT EXISTS fk_dobavljaci_batch
    FOREIGN KEY ("BatchId")
    REFERENCES "DataImportBatches"("Id")
    ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS idx_dobavljaci_batch
    ON "Dobavljaci"("BatchId")
    WHERE "BatchId" IS NOT NULL;

ALTER TABLE "Sezone"
    ADD COLUMN IF NOT EXISTS "BatchId" BIGINT;
ALTER TABLE "Sezone"
    ADD CONSTRAINT IF NOT EXISTS fk_sezone_batch
    FOREIGN KEY ("BatchId")
    REFERENCES "DataImportBatches"("Id")
    ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS idx_sezone_batch
    ON "Sezone"("BatchId")
    WHERE "BatchId" IS NOT NULL;

ALTER TABLE "TipoviObuce"
    ADD COLUMN IF NOT EXISTS "BatchId" BIGINT;
ALTER TABLE "TipoviObuce"
    ADD CONSTRAINT IF NOT EXISTS fk_tipoviobuce_batch
    FOREIGN KEY ("BatchId")
    REFERENCES "DataImportBatches"("Id")
    ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS idx_tipoviobuce_batch
    ON "TipoviObuce"("BatchId")
    WHERE "BatchId" IS NOT NULL;

ALTER TABLE "prodaja_zaglavlje"
    ADD COLUMN IF NOT EXISTS "BatchId" BIGINT;
ALTER TABLE "prodaja_zaglavlje"
    ADD CONSTRAINT IF NOT EXISTS fk_prodaja_zaglavlje_batch
    FOREIGN KEY ("BatchId")
    REFERENCES "DataImportBatches"("Id")
    ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS idx_prodaja_zaglavlje_batch
    ON "prodaja_zaglavlje"("BatchId")
    WHERE "BatchId" IS NOT NULL;

ALTER TABLE "prodaja_stavke"
    ADD COLUMN IF NOT EXISTS "BatchId" BIGINT;
ALTER TABLE "prodaja_stavke"
    ADD CONSTRAINT IF NOT EXISTS fk_prodaja_stavke_batch
    FOREIGN KEY ("BatchId")
    REFERENCES "DataImportBatches"("Id")
    ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS idx_prodaja_stavke_batch
    ON "prodaja_stavke"("BatchId")
    WHERE "BatchId" IS NOT NULL;

ALTER TABLE "dnevnik_promena"
    ADD COLUMN IF NOT EXISTS "BatchId" BIGINT;
ALTER TABLE "dnevnik_promena"
    ADD CONSTRAINT IF NOT EXISTS fk_dnevnik_promena_batch
    FOREIGN KEY ("BatchId")
    REFERENCES "DataImportBatches"("Id")
    ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS idx_dnevnik_promena_batch
    ON "dnevnik_promena"("BatchId")
    WHERE "BatchId" IS NOT NULL;

-- 4️⃣ delete_batch – POTPUNO REFAKTORISAN
CREATE OR REPLACE FUNCTION delete_batch(
    p_batch_id BIGINT,
    p_include_analytics BOOLEAN DEFAULT TRUE
)
RETURNS TABLE (
    batch_found BOOLEAN,
    total_deleted INTEGER
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_total_deleted INTEGER := 0;
    v_rowcount INTEGER;
BEGIN
    IF p_batch_id IS NULL THEN
        RAISE EXCEPTION 'BatchId cannot be NULL';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM "DataImportBatches"
        WHERE "Id" = p_batch_id
    ) THEN
        RETURN QUERY SELECT FALSE, 0;
        RETURN;
    END IF;

    -- 1️⃣ delete children first (avoid deadlock)

    IF to_regclass('prodaja_stavke') IS NOT NULL THEN
        DELETE FROM prodaja_stavke
        WHERE batch_id = p_batch_id;
        GET DIAGNOSTICS v_rowcount = ROW_COUNT;
        v_total_deleted := v_total_deleted + v_rowcount;
    END IF;

    IF to_regclass('prodaja_zaglavlje') IS NOT NULL THEN
        DELETE FROM prodaja_zaglavlje
        WHERE batch_id = p_batch_id;
        GET DIAGNOSTICS v_rowcount = ROW_COUNT;
        v_total_deleted := v_total_deleted + v_rowcount;
    END IF;

    IF to_regclass('dnevnik_promena') IS NOT NULL THEN
        DELETE FROM dnevnik_promena
        WHERE batch_id = p_batch_id;
        GET DIAGNOSTICS v_rowcount = ROW_COUNT;
        v_total_deleted := v_total_deleted + v_rowcount;
    END IF;

    -- 2️⃣ master tables

    DELETE FROM "Artikli" WHERE "BatchId" = p_batch_id;
    GET DIAGNOSTICS v_rowcount = ROW_COUNT;
    v_total_deleted := v_total_deleted + v_rowcount;

    DELETE FROM "Dobavljaci" WHERE "BatchId" = p_batch_id;
    GET DIAGNOSTICS v_rowcount = ROW_COUNT;
    v_total_deleted := v_total_deleted + v_rowcount;

    DELETE FROM "Sezone" WHERE "BatchId" = p_batch_id;
    GET DIAGNOSTICS v_rowcount = ROW_COUNT;
    v_total_deleted := v_total_deleted + v_rowcount;

    DELETE FROM "TipoviObuce" WHERE "BatchId" = p_batch_id;
    GET DIAGNOSTICS v_rowcount = ROW_COUNT;
    v_total_deleted := v_total_deleted + v_rowcount;

    -- 3️⃣ analytics cleanup

    IF p_include_analytics THEN
        IF to_regclass('products_dim') IS NOT NULL THEN
            DELETE FROM products_dim
            WHERE batch_id = p_batch_id
              AND data_origin = 'access';
            GET DIAGNOSTICS v_rowcount = ROW_COUNT;
            v_total_deleted := v_total_deleted + v_rowcount;
        END IF;

        IF to_regclass('sales_facts') IS NOT NULL THEN
            DELETE FROM sales_facts
            WHERE batch_id = p_batch_id
              AND data_origin = 'access';
            GET DIAGNOSTICS v_rowcount = ROW_COUNT;
            v_total_deleted := v_total_deleted + v_rowcount;
        END IF;

        IF to_regclass('sales_line_facts') IS NOT NULL THEN
            DELETE FROM sales_line_facts
            WHERE batch_id = p_batch_id
              AND data_origin = 'access';
            GET DIAGNOSTICS v_rowcount = ROW_COUNT;
            v_total_deleted := v_total_deleted + v_rowcount;
        END IF;
    END IF;

    -- 4️⃣ finally delete batch

    DELETE FROM "DataImportBatches"
    WHERE "Id" = p_batch_id;

    RETURN QUERY SELECT TRUE, v_total_deleted;
END;
$$;

-- 5️⃣ Trigger – optimizovan
CREATE OR REPLACE FUNCTION trg_batch_set_duration()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW."CompletedAtUtc" IS NOT NULL
       AND OLD."CompletedAtUtc" IS NULL
       AND NEW."StartedAtUtc" IS NOT NULL THEN

        NEW."DurationSeconds" :=
            EXTRACT(EPOCH FROM
                (NEW."CompletedAtUtc" - NEW."StartedAtUtc")
            )::INTEGER;
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_batch_duration
ON "DataImportBatches";

CREATE TRIGGER trg_batch_duration
BEFORE UPDATE ON "DataImportBatches"
FOR EACH ROW
EXECUTE FUNCTION trg_batch_set_duration();

-- Recommended indexes
CREATE INDEX IF NOT EXISTS idx_prodaja_stavke_batch
ON prodaja_stavke(batch_id);

CREATE INDEX IF NOT EXISTS idx_prodaja_zaglavlje_batch
ON prodaja_zaglavlje(batch_id);

CREATE INDEX IF NOT EXISTS idx_dnevnik_batch
ON dnevnik_promena(batch_id);

CREATE INDEX IF NOT EXISTS idx_salesfacts_batch_origin
ON sales_facts(batch_id, data_origin);
