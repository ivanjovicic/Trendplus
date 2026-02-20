-- Add ImagePath column to Artikli if missing and create index
ALTER TABLE "Artikli"
    ADD COLUMN IF NOT EXISTS "ImagePath" VARCHAR(500);

CREATE INDEX IF NOT EXISTS "IX_Artikli_ImagePath" ON "Artikli" ("ImagePath");
