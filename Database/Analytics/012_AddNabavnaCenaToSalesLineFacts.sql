-- ============================================================
-- Add NabavnaCena column to SalesLineFacts
-- Required by C# SalesLineFact model for gross margin calculation
-- Target DB: Analytics PostgreSQL database
-- Safe to run multiple times (IF NOT EXISTS)
-- ============================================================

ALTER TABLE "SalesLineFacts"
    ADD COLUMN IF NOT EXISTS "NabavnaCena" NUMERIC(18,2);

COMMENT ON COLUMN "SalesLineFacts"."NabavnaCena"
    IS 'Purchase price at time of sale, used for gross margin computation';

-- Insert migration history record
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260301000000_AddNabavnaCenaToSalesLineFacts', '8.0.22')
ON CONFLICT ("MigrationId") DO NOTHING;
