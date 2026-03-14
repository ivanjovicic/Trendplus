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
) PARTITION BY RANGE ("SaleTimestampUtc");

-- Define partitions for SalesFacts
CREATE TABLE "SalesFacts_2026" PARTITION OF "SalesFacts"
FOR VALUES FROM ('2026-01-01') TO ('2026-12-31');

-- Create unique index on SaleId (idempotency)
CREATE UNIQUE INDEX IF NOT EXISTS "IX_SalesFacts_SaleId" ON "SalesFacts" ("SaleId");

-- Create indexes for queries
CREATE INDEX IF NOT EXISTS "IX_SalesFacts_SaleTimestampUtc" ON "SalesFacts" ("SaleTimestampUtc");
CREATE INDEX IF NOT EXISTS "IX_SalesFacts_StoreId" ON "SalesFacts" ("StoreId");

-- Create SalesLineFacts table
CREATE TABLE IF NOT EXISTS "SalesLineFacts" (
    "Id" bigserial PRIMARY KEY,
    "SaleId" integer NOT NULL REFERENCES "SalesFacts" ("Id") ON DELETE CASCADE,
    "ProductId" integer NOT NULL,
    "Qty" integer NOT NULL,
    "UnitPrice" numeric(18,2) NOT NULL,
    "LineTotal" numeric(18,2) NOT NULL
) PARTITION BY RANGE ("SaleId");

-- Define partitions for SalesLineFacts
CREATE TABLE "SalesLineFacts_2026" PARTITION OF "SalesLineFacts"
FOR VALUES FROM (1) TO (1000000);

-- Create indexes for queries
CREATE INDEX IF NOT EXISTS "IX_SalesLineFacts_SaleId" ON "SalesLineFacts" ("SaleId");
CREATE INDEX IF NOT EXISTS "IX_SalesLineFacts_ProductId_SaleId" ON "SalesLineFacts" ("ProductId", "SaleId");

-- Add validation constraints
ALTER TABLE "SalesFacts"
ADD CONSTRAINT "CHK_PaymentType" CHECK ("PaymentType" IN ('Cash', 'Card', 'Online'));

ALTER TABLE "SalesFacts"
ADD CONSTRAINT "CHK_BrojRacuna" CHECK (LENGTH("BrojRacuna") > 0);

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
