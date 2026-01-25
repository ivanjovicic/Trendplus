-- Mark migration as applied without executing it
-- Run this script with: psql -U postgres -d trendplus_db -f fix_migration.sql

-- Insert migration record
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260112000000_AddArtikliKategorije', '8.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

-- Verify
SELECT "MigrationId", "ProductVersion" 
FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20260112000000_AddArtikliKategorije';

-- Show result
\echo 'Migration marked as applied!'
