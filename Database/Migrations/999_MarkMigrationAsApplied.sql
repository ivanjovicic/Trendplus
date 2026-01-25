-- Mark EF Core migration as applied without running it
-- This is needed because columns were already added via manual SQL scripts

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260112000000_AddArtikliKategorije', '8.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

-- Verify it was added
SELECT "MigrationId", "ProductVersion" 
FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20260112000000_AddArtikliKategorije';
