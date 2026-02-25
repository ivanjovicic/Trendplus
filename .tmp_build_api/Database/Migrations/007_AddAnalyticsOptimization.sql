-- ============================================
-- ANALYTICS OPTIMIZATION - Indexes & Pre-aggregated Tables
-- Run on: PostgreSQL (Neon)
-- ============================================

-- ============================================
-- PART 1: INDEXES FOR FASTER QUERIES
-- ============================================

-- Index on ProdajaZaglavlja.DatumProdaje (most important!)
CREATE INDEX IF NOT EXISTS idx_prodaja_datum 
ON "ProdajaZaglavlja" ("DatumProdaje" DESC);

-- Composite index for date range queries with status
CREATE INDEX IF NOT EXISTS idx_prodaja_datum_status 
ON "ProdajaZaglavlja" ("DatumProdaje", "Status");

-- Index on ProdajaStavke for JOIN operations
CREATE INDEX IF NOT EXISTS idx_prodaja_stavke_prodaja 
ON "ProdajaStavke" ("IdProdaja");

CREATE INDEX IF NOT EXISTS idx_prodaja_stavke_artikal 
ON "ProdajaStavke" ("IdArtikal");

-- Composite index for common JOIN pattern
CREATE INDEX IF NOT EXISTS idx_prodaja_stavke_prodaja_artikal 
ON "ProdajaStavke" ("IdProdaja", "IdArtikal");

-- Index on Artikli for category/supplier grouping
CREATE INDEX IF NOT EXISTS idx_artikli_kategorija 
ON "Artikli" ("Kategorija");

CREATE INDEX IF NOT EXISTS idx_artikli_dobavljac 
ON "Artikli" ("IDDobavljac");

CREATE INDEX IF NOT EXISTS idx_artikli_pol 
ON "Artikli" ("Pol");

-- Composite index for category + supplier analytics
CREATE INDEX IF NOT EXISTS idx_artikli_kategorija_dobavljac 
ON "Artikli" ("Kategorija", "IDDobavljac");

-- Index on Dobavljaci
CREATE INDEX IF NOT EXISTS idx_dobavljaci_naziv 
ON "Dobavljaci" ("Naziv");

-- ============================================
-- PART 2: PRE-AGGREGATED TABLES
-- ============================================

-- Daily Sales Summary (pre-computed daily aggregates)
CREATE TABLE IF NOT EXISTS "AnalyticsDailySummary" (
    "Id" SERIAL PRIMARY KEY,
    "Date" DATE NOT NULL UNIQUE,
    "TotalRevenue" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "TotalTransactions" INT NOT NULL DEFAULT 0,
    "TotalUnits" INT NOT NULL DEFAULT 0,
    "AvgBasketValue" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "AvgItemPrice" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "UpdatedAt" TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_daily_summary_date 
ON "AnalyticsDailySummary" ("Date" DESC);

-- Category Summary (pre-computed category aggregates)
CREATE TABLE IF NOT EXISTS "AnalyticsCategorySummary" (
    "Id" SERIAL PRIMARY KEY,
    "Date" DATE NOT NULL,
    "Kategorija" VARCHAR(100) NOT NULL,
    "TotalRevenue" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "TotalUnits" INT NOT NULL DEFAULT 0,
    "TransactionCount" INT NOT NULL DEFAULT 0,
    "UpdatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE("Date", "Kategorija")
);

CREATE INDEX IF NOT EXISTS idx_category_summary_date 
ON "AnalyticsCategorySummary" ("Date" DESC);

CREATE INDEX IF NOT EXISTS idx_category_summary_kategorija 
ON "AnalyticsCategorySummary" ("Kategorija");

-- Supplier Summary (pre-computed supplier aggregates)
CREATE TABLE IF NOT EXISTS "AnalyticsSupplierSummary" (
    "Id" SERIAL PRIMARY KEY,
    "Date" DATE NOT NULL,
    "DobavljacId" INT,
    "DobavljacNaziv" VARCHAR(200),
    "TotalRevenue" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "TotalUnits" INT NOT NULL DEFAULT 0,
    "TransactionCount" INT NOT NULL DEFAULT 0,
    "UpdatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE("Date", "DobavljacId")
);

CREATE INDEX IF NOT EXISTS idx_supplier_summary_date 
ON "AnalyticsSupplierSummary" ("Date" DESC);

CREATE INDEX IF NOT EXISTS idx_supplier_summary_dobavljac 
ON "AnalyticsSupplierSummary" ("DobavljacId");

-- Gender Summary (pre-computed gender aggregates)
CREATE TABLE IF NOT EXISTS "AnalyticsGenderSummary" (
    "Id" SERIAL PRIMARY KEY,
    "Date" DATE NOT NULL,
    "Pol" VARCHAR(50) NOT NULL,
    "TotalRevenue" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "TotalUnits" INT NOT NULL DEFAULT 0,
    "UpdatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE("Date", "Pol")
);

CREATE INDEX IF NOT EXISTS idx_gender_summary_date 
ON "AnalyticsGenderSummary" ("Date" DESC);

-- Top Products Summary (daily top products cache)
CREATE TABLE IF NOT EXISTS "AnalyticsTopProducts" (
    "Id" SERIAL PRIMARY KEY,
    "Date" DATE NOT NULL,
    "ProductId" INT NOT NULL,
    "ProductName" VARCHAR(300),
    "TotalRevenue" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "TotalUnits" INT NOT NULL DEFAULT 0,
    "Rank" INT NOT NULL DEFAULT 0,
    "UpdatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE("Date", "ProductId")
);

CREATE INDEX IF NOT EXISTS idx_top_products_date 
ON "AnalyticsTopProducts" ("Date" DESC);

CREATE INDEX IF NOT EXISTS idx_top_products_rank 
ON "AnalyticsTopProducts" ("Date", "Rank");

-- ============================================
-- PART 3: STORED PROCEDURE FOR REFRESHING AGGREGATES
-- ============================================

-- Function to refresh daily summary for a specific date
CREATE OR REPLACE FUNCTION refresh_daily_summary(target_date DATE)
RETURNS VOID AS $$
BEGIN
    INSERT INTO "AnalyticsDailySummary" ("Date", "TotalRevenue", "TotalTransactions", "TotalUnits", "AvgBasketValue", "AvgItemPrice", "UpdatedAt")
    SELECT 
        target_date,
        COALESCE(SUM(ps."Kolicina" * ps."Cena"), 0),
        COUNT(DISTINCT p."Id"),
        COALESCE(SUM(ps."Kolicina"), 0),
        CASE WHEN COUNT(DISTINCT p."Id") > 0 
            THEN COALESCE(SUM(ps."Kolicina" * ps."Cena"), 0) / COUNT(DISTINCT p."Id")
            ELSE 0 
        END,
        CASE WHEN SUM(ps."Kolicina") > 0 
            THEN COALESCE(SUM(ps."Kolicina" * ps."Cena"), 0) / SUM(ps."Kolicina")
            ELSE 0 
        END,
        NOW()
    FROM "ProdajaZaglavlja" p
    JOIN "ProdajaStavke" ps ON p."Id" = ps."IdProdaja"
    WHERE DATE(p."DatumProdaje") = target_date
    ON CONFLICT ("Date") DO UPDATE SET
        "TotalRevenue" = EXCLUDED."TotalRevenue",
        "TotalTransactions" = EXCLUDED."TotalTransactions",
        "TotalUnits" = EXCLUDED."TotalUnits",
        "AvgBasketValue" = EXCLUDED."AvgBasketValue",
        "AvgItemPrice" = EXCLUDED."AvgItemPrice",
        "UpdatedAt" = NOW();
END;
$$ LANGUAGE plpgsql;

-- Function to refresh category summary for a specific date
CREATE OR REPLACE FUNCTION refresh_category_summary(target_date DATE)
RETURNS VOID AS $$
BEGIN
    DELETE FROM "AnalyticsCategorySummary" WHERE "Date" = target_date;
    
    INSERT INTO "AnalyticsCategorySummary" ("Date", "Kategorija", "TotalRevenue", "TotalUnits", "TransactionCount", "UpdatedAt")
    SELECT 
        target_date,
        COALESCE(a."Kategorija", 'Nepoznato'),
        COALESCE(SUM(ps."Kolicina" * ps."Cena"), 0),
        COALESCE(SUM(ps."Kolicina"), 0),
        COUNT(DISTINCT p."Id"),
        NOW()
    FROM "ProdajaZaglavlja" p
    JOIN "ProdajaStavke" ps ON p."Id" = ps."IdProdaja"
    JOIN "Artikli" a ON ps."IdArtikal" = a."Id"
    WHERE DATE(p."DatumProdaje") = target_date
    GROUP BY a."Kategorija";
END;
$$ LANGUAGE plpgsql;

-- Function to refresh supplier summary for a specific date
CREATE OR REPLACE FUNCTION refresh_supplier_summary(target_date DATE)
RETURNS VOID AS $$
BEGIN
    DELETE FROM "AnalyticsSupplierSummary" WHERE "Date" = target_date;
    
    INSERT INTO "AnalyticsSupplierSummary" ("Date", "DobavljacId", "DobavljacNaziv", "TotalRevenue", "TotalUnits", "TransactionCount", "UpdatedAt")
    SELECT 
        target_date,
        d."Id",
        COALESCE(d."Naziv", 'Nepoznato'),
        COALESCE(SUM(ps."Kolicina" * ps."Cena"), 0),
        COALESCE(SUM(ps."Kolicina"), 0),
        COUNT(DISTINCT p."Id"),
        NOW()
    FROM "ProdajaZaglavlja" p
    JOIN "ProdajaStavke" ps ON p."Id" = ps."IdProdaja"
    JOIN "Artikli" a ON ps."IdArtikal" = a."Id"
    LEFT JOIN "Dobavljaci" d ON a."IDDobavljac" = d."Id"
    WHERE DATE(p."DatumProdaje") = target_date
    GROUP BY d."Id", d."Naziv";
END;
$$ LANGUAGE plpgsql;

-- Function to refresh all summaries for a date range
CREATE OR REPLACE FUNCTION refresh_all_analytics(from_date DATE, to_date DATE)
RETURNS VOID AS $$
DECLARE
    current_date DATE := from_date;
BEGIN
    WHILE current_date <= to_date LOOP
        PERFORM refresh_daily_summary(current_date);
        PERFORM refresh_category_summary(current_date);
        PERFORM refresh_supplier_summary(current_date);
        current_date := current_date + INTERVAL '1 day';
    END LOOP;
END;
$$ LANGUAGE plpgsql;

-- ============================================
-- PART 4: INITIAL DATA POPULATION
-- ============================================

-- Populate daily summary for last 90 days
DO $$
DECLARE
    d DATE;
BEGIN
    FOR d IN SELECT generate_series(CURRENT_DATE - INTERVAL '90 days', CURRENT_DATE, '1 day')::DATE
    LOOP
        PERFORM refresh_daily_summary(d);
        PERFORM refresh_category_summary(d);
        PERFORM refresh_supplier_summary(d);
    END LOOP;
END $$;

-- ============================================
-- VERIFICATION
-- ============================================
SELECT 'Indexes created' AS status;
SELECT 'Pre-aggregated tables created' AS status;
SELECT COUNT(*) AS daily_summary_count FROM "AnalyticsDailySummary";
SELECT COUNT(*) AS category_summary_count FROM "AnalyticsCategorySummary";
SELECT COUNT(*) AS supplier_summary_count FROM "AnalyticsSupplierSummary";
