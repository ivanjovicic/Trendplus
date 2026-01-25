# Mark EF Core migration as applied
# Run this from PowerShell

$env:PGPASSWORD = "your_password_here"  # Set your PostgreSQL password

$sql = @"
INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\")
VALUES ('20260112000000_AddArtikliKategorije', '8.0.0')
ON CONFLICT (\"MigrationId\") DO NOTHING;

SELECT 'Migration marked as applied' AS result;
"@

# Execute SQL
psql -U postgres -d trendplus_db -c $sql

Write-Host "Done! Now run: dotnet ef database update" -ForegroundColor Green
