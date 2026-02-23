-- Analytics DB: mark row origin for imported vs existing data
-- Safe to run multiple times.

ALTER TABLE "ProductsDim"
    ADD COLUMN IF NOT EXISTS "DataOrigin" VARCHAR(32) NOT NULL DEFAULT 'existing';

ALTER TABLE "SalesFacts"
    ADD COLUMN IF NOT EXISTS "DataOrigin" VARCHAR(32) NOT NULL DEFAULT 'existing';

ALTER TABLE "SalesLineFacts"
    ADD COLUMN IF NOT EXISTS "DataOrigin" VARCHAR(32) NOT NULL DEFAULT 'existing';

CREATE INDEX IF NOT EXISTS "IX_ProductsDim_DataOrigin" ON "ProductsDim" ("DataOrigin");
CREATE INDEX IF NOT EXISTS "IX_SalesFacts_DataOrigin" ON "SalesFacts" ("DataOrigin");
CREATE INDEX IF NOT EXISTS "IX_SalesLineFacts_DataOrigin" ON "SalesLineFacts" ("DataOrigin");
