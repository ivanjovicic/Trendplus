-- Analytics Database: Create SalesFacts and SalesLineFacts tables
-- Run this script against your analytics PostgreSQL database

-- Create SalesFacts table
CREATE TABLE IF NOT EXISTS "SalesFacts" (
    "Id" bigserial PRIMARY KEY,
    "SaleId" integer NOT NULL,
    "BrojRacuna" varchar(100) NOT NULL,
    "SaleTimestampUtc" timestamp with time zone NOT NULL,
    "StoreId" integer NOT NULL,
    "PaymentType" varchar(100) NOT NULL,
    "TotalAmount" numeric(18,2) NOT NULL,
    "TotalUnits" integer NOT NULL,
    "TotalLines" integer NOT NULL
);

-- Create unique index on SaleId (idempotency)
CREATE UNIQUE INDEX IF NOT EXISTS "IX_SalesFacts_SaleId" ON "SalesFacts" ("SaleId");

-- Create indexes for queries
CREATE INDEX IF NOT EXISTS "IX_SalesFacts_SaleTimestampUtc" ON "SalesFacts" ("SaleTimestampUtc");
CREATE INDEX IF NOT EXISTS "IX_SalesFacts_StoreId" ON "SalesFacts" ("StoreId");

-- Create SalesLineFacts table
CREATE TABLE IF NOT EXISTS "SalesLineFacts" (
    "Id" bigserial PRIMARY KEY,
    "SaleId" integer NOT NULL,
    "ProductId" integer NOT NULL,
    "Qty" integer NOT NULL,
    "UnitPrice" numeric(18,2) NOT NULL,
    "LineTotal" numeric(18,2) NOT NULL
);

-- Create indexes for queries
CREATE INDEX IF NOT EXISTS "IX_SalesLineFacts_SaleId" ON "SalesLineFacts" ("SaleId");
CREATE INDEX IF NOT EXISTS "IX_SalesLineFacts_ProductId_SaleId" ON "SalesLineFacts" ("ProductId", "SaleId");

-- Create __EFMigrationsHistory table if it doesn't exist (for EF Core tracking)
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" varchar(150) PRIMARY KEY,
    "ProductVersion" varchar(32) NOT NULL
);

-- Insert migration history record
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260110170000_AddSalesFacts', '8.0.22')
ON CONFLICT ("MigrationId") DO NOTHING;

-- Verify tables created
SELECT 
    'Table exists: ' || table_name as status
FROM information_schema.tables
WHERE table_schema = 'public'
AND table_name IN ('SalesFacts', 'SalesLineFacts')
ORDER BY table_name;

-- Show table counts
SELECT 
    'SalesFacts' as table_name,
    COUNT(*) as row_count
FROM "SalesFacts"
UNION ALL
SELECT 
    'SalesLineFacts' as table_name,
    COUNT(*) as row_count
FROM "SalesLineFacts";
