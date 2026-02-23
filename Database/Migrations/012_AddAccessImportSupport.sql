-- Trendplus: support importing data from MS Access (.accdb)
-- Safe to run multiple times.

-- Core metadata table for import history
CREATE TABLE IF NOT EXISTS "DataImportBatches" (
    "Id" BIGSERIAL PRIMARY KEY,
    "SourceSystem" VARCHAR(64) NOT NULL DEFAULT 'access',
    "SourceFileName" VARCHAR(300) NOT NULL,
    "StartedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "CompletedAtUtc" TIMESTAMPTZ NULL,
    "Status" VARCHAR(32) NOT NULL DEFAULT 'running',
    "SummaryJson" TEXT NULL,
    "ErrorMessage" VARCHAR(4000) NULL
);

CREATE INDEX IF NOT EXISTS "IX_DataImportBatches_StartedAtUtc" ON "DataImportBatches" ("StartedAtUtc");
CREATE INDEX IF NOT EXISTS "IX_DataImportBatches_Status" ON "DataImportBatches" ("Status");

-- Data origin flags for global view scope switching (existing/imported/all)
ALTER TABLE "Artikli"
    ADD COLUMN IF NOT EXISTS "DataOrigin" VARCHAR(32) NOT NULL DEFAULT 'existing';

ALTER TABLE "Dobavljaci"
    ADD COLUMN IF NOT EXISTS "DataOrigin" VARCHAR(32) NOT NULL DEFAULT 'existing';

ALTER TABLE "Sezone"
    ADD COLUMN IF NOT EXISTS "DataOrigin" VARCHAR(32) NOT NULL DEFAULT 'existing';

ALTER TABLE "TipoviObuce"
    ADD COLUMN IF NOT EXISTS "DataOrigin" VARCHAR(32) NOT NULL DEFAULT 'existing';

ALTER TABLE IF EXISTS prodaja_zaglavlje
    ADD COLUMN IF NOT EXISTS data_origin VARCHAR(32) NOT NULL DEFAULT 'existing';

CREATE INDEX IF NOT EXISTS "IX_Artikli_DataOrigin" ON "Artikli" ("DataOrigin");
CREATE INDEX IF NOT EXISTS "IX_Dobavljaci_DataOrigin" ON "Dobavljaci" ("DataOrigin");
CREATE INDEX IF NOT EXISTS "IX_Sezone_DataOrigin" ON "Sezone" ("DataOrigin");
CREATE INDEX IF NOT EXISTS "IX_TipoviObuce_DataOrigin" ON "TipoviObuce" ("DataOrigin");
CREATE INDEX IF NOT EXISTS "IX_prodaja_zaglavlje_data_origin" ON prodaja_zaglavlje (data_origin);
